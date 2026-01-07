using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FileRecord.Data;
using FileRecord.Models;
using FileRecord.Utils;

namespace FileRecord.Services.Upload
{
    public class UploadTask
    {
        public int FileId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string MonitorGroupId { get; set; } = string.Empty; // 用于标识上传目标配置
        public int RetryCount { get; set; } = 0;
        public DateTime? LastAttempt { get; set; } = null;
        public string? ErrorMessage { get; set; }
    }

    public class UploadTaskManager
    {
        private readonly ConcurrentQueue<UploadTask> _uploadQueue;
        private readonly DatabaseContext _databaseContext;
        private readonly string _backupDirectory;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Dictionary<string, IUploadTarget> _uploadTargets;
        private Task? _workerTask;
        
        // 默认上传目标配置
        private readonly UploadTargetConfig _defaultConfig;

        public UploadTaskManager(DatabaseContext databaseContext, string backupDirectory = "bak")
        {
            _uploadQueue = new ConcurrentQueue<UploadTask>();
            _databaseContext = databaseContext;
            _backupDirectory = backupDirectory;
            _cancellationTokenSource = new CancellationTokenSource();
            _uploadTargets = new Dictionary<string, IUploadTarget>();
            
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
            // 从数据库获取文件信息以获取MonitorGroupId
            var fileInfo = GetFileInfoById(fileId);
            var task = new UploadTask
            {
                FileId = fileId,
                FilePath = filePath,
                MonitorGroupId = fileInfo?.MonitorGroupId ?? "default"
            };
            _uploadQueue.Enqueue(task);
        }

        public void StartProcessing()
        {
            _workerTask = Task.Run(ProcessUploadQueue);
        }

        public void StopProcessing()
        {
            _cancellationTokenSource.Cancel();
            try
            {
                _workerTask?.Wait(TimeSpan.FromSeconds(5));
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
                    // 上传失败，如果重试次数未达到上限，则重新加入队列
                    if (task.RetryCount < 3) // 最大重试3次
                    {
                        Console.WriteLine($"文件上传失败，准备重试({task.RetryCount}/3): {task.FilePath} - {task.ErrorMessage}");
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, task.RetryCount))); // 指数退避
                        _uploadQueue.Enqueue(task);
                    }
                    else
                    {
                        Console.WriteLine($"文件上传失败，已达最大重试次数: {task.FilePath} - {task.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                task.ErrorMessage = ex.Message;
                if (task.RetryCount < 3)
                {
                    Console.WriteLine($"文件上传异常，准备重试({task.RetryCount}/3): {task.FilePath} - {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, task.RetryCount)));
                    _uploadQueue.Enqueue(task);
                }
                else
                {
                    Console.WriteLine($"文件上传异常，已达最大重试次数: {task.FilePath} - {ex.Message}");
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
                string targetKey = string.IsNullOrEmpty(task.MonitorGroupId) ? "default" : task.MonitorGroupId;
                if (!_uploadTargets.ContainsKey(targetKey))
                {
                    Console.WriteLine($"未找到上传目标配置: {targetKey}，使用默认配置");
                    targetKey = "default";
                }
                        
                var uploadTarget = _uploadTargets[targetKey];
                        
                // 计算相对路径
                var relativePath = System.IO.Path.GetRelativePath(fileInfoFromDb?.DirectoryPath ?? fileInfo.DirectoryName ?? ".", task.FilePath);
                        
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
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_databaseContext.GetConnectionString());
            connection.Open();
            
            var selectSql = "SELECT Id, FileName, FilePath, FileSize, CreatedTime, ModifiedTime, Extension, DirectoryPath, MonitorGroupId, IsUploaded, UploadTime, MD5Hash, IsDeleted FROM FileInfos WHERE Id = @Id";
            
            using var command = new Microsoft.Data.Sqlite.SqliteCommand(selectSql, connection);
            command.Parameters.AddWithValue("@Id", fileId);
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new FileInfoModel
                {
                    Id = reader.GetInt32(0),
                    FileName = reader.GetString(1),
                    FilePath = reader.GetString(2),
                    FileSize = reader.GetInt64(3),
                    CreatedTime = DateTime.Parse(reader.GetString(4)),
                    ModifiedTime = DateTime.Parse(reader.GetString(5)),
                    Extension = reader.GetString(6),
                    DirectoryPath = reader.GetString(7),
                    MonitorGroupId = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    IsUploaded = reader.GetInt32(9) == 1,
                    UploadTime = reader.IsDBNull(10) ? (DateTime?)null : DateTime.Parse(reader.GetString(10)),
                    MD5Hash = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                    IsDeleted = reader.GetInt32(12) == 1
                };
            }
            
            return null;
        }
    }
}