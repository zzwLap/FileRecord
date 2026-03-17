using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileRecord.Config;
using FileRecord.Data;
using FileRecord.Models;
using FileRecord.Utils;

#if NET472
using PriorityQueueType = FileRecord.Utils.CompatiblePriorityQueue<FileRecord.Services.Upload.UploadTask, System.DateTime>;
#else
using PriorityQueueType = System.Collections.Generic.PriorityQueue<FileRecord.Services.Upload.UploadTask, System.DateTime>;
#endif

namespace FileRecord.Services.Upload
{
    public class UploadTask
    {
        public int FileId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string MonitorGroupId { get; set; } = string.Empty; // 文件分类组（业务分组）
        public string UploadTargetId { get; set; } = string.Empty; // 实际上传目标
        public int RetryCount { get; set; } = 0;
        public DateTime? LastAttempt { get; set; } = null;
        public string? ErrorMessage { get; set; }
        public DateTime? NextRetryTime { get; set; } = null; // 下次重试时间
    }

    /// <summary>
    /// 上传目标选择委托 - 根据文件信息返回目标ID
    /// </summary>
    public delegate string UploadTargetSelector(FileInfoModel fileInfo);

    public class UploadTaskManager
    {
        private readonly ConcurrentQueue<UploadTask> _uploadQueue;
        private readonly DatabaseContext _databaseContext;
        private readonly string _backupDirectory;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Dictionary<string, IUploadTarget> _uploadTargets;
        private Task? _workerTask;
        private Task? _schedulerTask;
        
        // 用于调度等待重试的任务
        private readonly PriorityQueueType _scheduledRetryQueue;
        private readonly SemaphoreSlim _queueSemaphore;
        
        // 默认上传目标配置
        private readonly UploadTargetConfig _defaultConfig;
        // 定时重试间隔（分钟）
        private readonly int _retryIntervalMinutes = AppConfig.DefaultRetryIntervalMinutes;
        
        /// <summary>
        /// 自定义上传目标选择方法，为空则使用默认逻辑
        /// </summary>
        public UploadTargetSelector? CustomTargetSelector { get; set; }

        public UploadTaskManager(DatabaseContext databaseContext, string backupDirectory = "bak", int retryIntervalMinutes = 30)
        {
            _uploadQueue = new ConcurrentQueue<UploadTask>();
            _databaseContext = databaseContext;
            _backupDirectory = backupDirectory;
            _cancellationTokenSource = new CancellationTokenSource();
            _uploadTargets = new Dictionary<string, IUploadTarget>();
            _retryIntervalMinutes = retryIntervalMinutes;
            
            // 初始化调度队列
            _scheduledRetryQueue = new PriorityQueueType();
            _queueSemaphore = new SemaphoreSlim(1, 1);
            
            // 初始化默认配置
            _defaultConfig = new UploadTargetConfig
            {
                Id = "default",
                Name = "默认本地备份",
                TargetType = UploadTargetType.Local,
                TargetPath = _backupDirectory
            };
            
            // 确保备份目录存在
            if (!System.IO.Directory.Exists(_backupDirectory))
            {
                System.IO.Directory.CreateDirectory(_backupDirectory);
            }
            
            // 添加默认上传目标
            var defaultTarget = new LocalUploadTarget();
            defaultTarget.InitializeAsync(_defaultConfig).Wait();
            _uploadTargets["default"] = defaultTarget;
        }

        public void EnqueueUpload(int fileId, string filePath)
        {
            // 从数据库获取文件信息
            var fileInfo = GetFileInfoById(fileId);
            
            // 使用自定义选择器或默认逻辑
            var targetId = CustomTargetSelector?.Invoke(fileInfo!) ?? fileInfo?.MonitorGroupId ?? "default";
            
            var task = new UploadTask
            {
                FileId = fileId,
                FilePath = filePath,
                MonitorGroupId = fileInfo?.MonitorGroupId ?? "default",
                UploadTargetId = targetId,
                NextRetryTime = DateTime.Now // 立即可以重试
            };
            _uploadQueue.Enqueue(task);
        }

        public void StartProcessing()
        {
            _workerTask = Task.Run(ProcessUploadQueue);
            _schedulerTask = Task.Run(ScheduleRetryTasks);
        }

        public void StopProcessing()
        {
            _cancellationTokenSource.Cancel();
            try
            {
                var tasks = new List<Task>();
                if (_workerTask != null) tasks.Add(_workerTask);
                if (_schedulerTask != null) tasks.Add(_schedulerTask);
                if (tasks.Count > 0)
                {
                    Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(10));
                }
            }
            catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0] is TaskCanceledException)
            {
                // 忽略任务取消异常
            }
            catch (TaskCanceledException)
            {
                // 忽略任务取消异常
            }
        }
        
        // 调度重试任务
        private async Task ScheduleRetryTasks()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // 检查是否有到时间的重试任务
                    await _queueSemaphore.WaitAsync(_cancellationTokenSource.Token);
                    try
                    {
                        // 检查队列头部是否有到达重试时间的任务
                        if (_scheduledRetryQueue.Count > 0)
                        {
                            // 尝试获取队列头部的元素
                            if (TryPeekQueue(out var task, out var scheduledTime))
                            {
                                if (scheduledTime <= DateTime.Now)
                                {
                                    // 到达重试时间，将任务移除并加入主队列
                                    if (_scheduledRetryQueue.TryDequeue(out var dequeuedTask, out _) && dequeuedTask != null)
                                    {
                                        _uploadQueue.Enqueue(dequeuedTask);
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        _queueSemaphore.Release();
                    }
                    
                    // 等待一段时间再检查
                    await Task.Delay(1000, _cancellationTokenSource.Token); // 每秒检查一次
                }
                catch (OperationCanceledException)
                {
                    // 任务被取消，正常退出
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"调度重试任务时发生错误: {ex.Message}");
                }
            }
        }
        
        // 尝试查看队列头部元素而不移除它
        private bool TryPeekQueue(out UploadTask? task, out DateTime priority)
        {
            if (_scheduledRetryQueue.Count == 0)
            {
                task = null;
                priority = DateTime.MaxValue;
                return false;
            }
            
            // .NET 6+ PriorityQueue 支持 Peek
            task = _scheduledRetryQueue.Peek();
            // 获取优先级需要通过 UnorderedItems 查找
            priority = _scheduledRetryQueue.UnorderedItems.First().Priority;
            return true;
        }
        
        private async Task ProcessUploadQueue()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (_uploadQueue.TryDequeue(out var task))
                {
                    await ProcessUploadTask(task);
                }
                else
                {
                    // 队列为空，等待一段时间再检查
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
        }

        private async Task ProcessUploadTask(UploadTask task)
        {
            try
            {
                task.RetryCount++;
                task.LastAttempt = DateTime.Now;

                // 执行上传操作
                bool success = await PerformUpload(task);

                if (success)
                {
                    // 上传成功，更新数据库状态
                    _databaseContext.MarkFileAsUploaded(task.FileId, DateTime.Now);
                    Console.WriteLine($"文件上传成功: {task.FilePath}");
                }
                else
                {
                    await HandleUploadFailure(task, task.ErrorMessage ?? "上传失败");
                }
            }
            catch (Exception ex)
            {
                await HandleUploadFailure(task, ex.Message, true);
            }
        }
        
        /// <summary>
        /// 统一处理上传失败逻辑
        /// </summary>
        private async Task HandleUploadFailure(UploadTask task, string errorMessage, bool isException = false)
        {
            task.ErrorMessage = errorMessage;
            string errorType = isException ? "异常" : "失败";
            
            if (task.RetryCount < AppConfig.MaxImmediateRetryCount)
            {
                // 立即重试阶段：使用指数退避
                Console.WriteLine($"文件上传{errorType}，准备重试({task.RetryCount}/{AppConfig.MaxImmediateRetryCount}): {task.FilePath} - {errorMessage}");
                var delay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(task.RetryCount, AppConfig.MaxBackoffExponent)));
                task.NextRetryTime = DateTime.Now.Add(delay);
            }
            else
            {
                // 周期性重试阶段
                Console.WriteLine($"文件上传{errorType}，开始周期性重试({_retryIntervalMinutes}分钟间隔): {task.FilePath} - {errorMessage}");
                task.NextRetryTime = DateTime.Now.AddMinutes(_retryIntervalMinutes);
                task.ErrorMessage = $"周期性重试: {errorMessage}";
            }
            
            // 将任务添加到调度队列
            await AddToScheduledRetryQueue(task);
        }
        
        // 将任务添加到调度重试队列
        private async Task AddToScheduledRetryQueue(UploadTask task)
        {
            if (task.NextRetryTime.HasValue)
            {
                await _queueSemaphore.WaitAsync();
                try
                {
                    _scheduledRetryQueue.Enqueue(task, task.NextRetryTime.Value);
                }
                finally
                {
                    _queueSemaphore.Release();
                }
            }
        }

        private async Task<bool> PerformUpload(UploadTask task)
        {
            try
            {
                // 检查文件是否存在
                if (!System.IO.File.Exists(task.FilePath))
                {
                    task.ErrorMessage = "源文件不存在";
                    return false;
                }
                        
                // 从数据库获取文件信息，检查是否被标记为删除
                var fileInfoFromDb = GetFileInfoById(task.FileId);
                if (fileInfoFromDb != null && fileInfoFromDb.IsDeleted)
                {
                    Console.WriteLine($"文件已标记为删除，跳过上传: {task.FilePath}");
                    return true; // 返回true表示任务完成（虽然实际上没有上传）
                }
                        
                var fileInfo = new System.IO.FileInfo(task.FilePath);
                        
                // 确定使用的上传目标
                string targetKey = string.IsNullOrEmpty(task.UploadTargetId) ? "default" : task.UploadTargetId;
                
                // 如果目标不存在，尝试重新计算
                if (!_uploadTargets.ContainsKey(targetKey))
                {
                    Console.WriteLine($"未找到上传目标配置: {targetKey}，尝试重新计算...");
                    
                    // 使用自定义选择器重新计算，或回退到默认
                    targetKey = CustomTargetSelector?.Invoke(fileInfoFromDb!) ?? fileInfoFromDb?.MonitorGroupId ?? "default";
                    
                    // 更新任务的目标ID
                    task.UploadTargetId = targetKey;
                    
                    // 再次检查
                    if (!_uploadTargets.ContainsKey(targetKey))
                    {
                        Console.WriteLine($"重新计算后仍未找到目标: {targetKey}，使用默认配置");
                        targetKey = "default";
                        task.UploadTargetId = "default";
                    }
                    else
                    {
                        Console.WriteLine($"已重新计算上传目标: {targetKey}");
                    }
                }
                        
                var uploadTarget = _uploadTargets[targetKey];
                        
                // 计算相对路径
#if NET472
                var relativePath = ParallelCompat.GetRelativePath(fileInfoFromDb?.DirectoryPath ?? fileInfo.DirectoryName ?? ".", task.FilePath);
#else
                var relativePath = System.IO.Path.GetRelativePath(fileInfoFromDb?.DirectoryPath ?? fileInfo.DirectoryName ?? ".", task.FilePath);
#endif
                        
                // 执行上传
                bool success = await uploadTarget.UploadFileAsync(task.FilePath, relativePath);
                        
                return success;
            }
            catch (Exception ex)
            {
                task.ErrorMessage = ex.Message;
                return false;
            }
        }
        
        /// <summary>
        /// 添加上传目标配置
        /// </summary>
        /// <param name="config">上传目标配置</param>
        public async Task AddUploadTargetAsync(UploadTargetConfig config)
        {
            var uploadTarget = await UploadTargetFactory.CreateAndInitializeUploadTargetAsync(config);
            _uploadTargets[config.Id] = uploadTarget;
        }
        
        /// <summary>
        /// 移除上传目标配置
        /// </summary>
        /// <param name="targetId">目标ID</param>
        public void RemoveUploadTarget(string targetId)
        {
            if (_uploadTargets.ContainsKey(targetId))
            {
                _uploadTargets.Remove(targetId);
            }
        }
        
        /// <summary>
        /// 获取上传目标配置
        /// </summary>
        /// <param name="targetId">目标ID</param>
        /// <returns>上传目标配置，如果不存在返回null</returns>
        public IUploadTarget? GetUploadTarget(string targetId)
        {
            return _uploadTargets.ContainsKey(targetId) ? _uploadTargets[targetId] : null;
        }

        public void EnqueueUnuploadedFiles()
        {
            var unuploadedFiles = _databaseContext.GetUnuploadedFiles();
            foreach (var file in unuploadedFiles)
            {
                EnqueueUpload(file.Id, file.FilePath);
            }
        }
        
        // 从数据库获取特定ID的文件信息
        private FileInfoModel? GetFileInfoById(int fileId)
        {
            return _databaseContext.GetFileInfoById(fileId);
        }
    }
}