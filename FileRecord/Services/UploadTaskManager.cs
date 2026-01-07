using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FileRecord.Data;
using FileRecord.Models;

namespace FileRecord.Services.Upload
{
    public class UploadTask
    {
        public int FileId { get; set; }
        public string FilePath { get; set; } = string.Empty;
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
        private Task? _workerTask;

        public UploadTaskManager(DatabaseContext databaseContext, string backupDirectory = "bak")
        {
            _uploadQueue = new ConcurrentQueue<UploadTask>();
            _databaseContext = databaseContext;
            _backupDirectory = backupDirectory;
            _cancellationTokenSource = new CancellationTokenSource();

            // 确保备份目录存在
            if (!System.IO.Directory.Exists(_backupDirectory))
            {
                System.IO.Directory.CreateDirectory(_backupDirectory);
            }
        }

        public void EnqueueUpload(int fileId, string filePath)
        {
            var task = new UploadTask
            {
                FileId = fileId,
                FilePath = filePath
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
                    if (task.RetryCount < 3) // 最多重试3次
                    {
                        Console.WriteLine($"文件上传失败，准备重试 ({task.RetryCount}/3): {task.FilePath} - {task.ErrorMessage}");
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
                    Console.WriteLine($"文件上传异常，准备重试 ({task.RetryCount}/3): {task.FilePath} - {ex.Message}");
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
                
                // 确定目标路径 - 在备份目录下保持原始目录结构
                string targetDirectory = System.IO.Path.Combine(_backupDirectory, 
                    fileInfo.DirectoryName?.Replace(System.IO.Path.GetPathRoot(fileInfo.DirectoryName) ?? "", "").TrimStart('\\', '/') ?? "");
                
                if (!System.IO.Directory.Exists(targetDirectory))
                {
                    System.IO.Directory.CreateDirectory(targetDirectory);
                }
                
                string targetPath = System.IO.Path.Combine(targetDirectory, fileInfo.Name);
                
                // 复制文件到备份目录
                System.IO.File.Copy(task.FilePath, targetPath, true);
                
                return true;
            }
            catch (Exception ex)
            {
                task.ErrorMessage = ex.Message;
                return false;
            }
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
            
            var selectSql = "SELECT Id, FileName, FilePath, FileSize, CreatedTime, ModifiedTime, Extension, DirectoryPath, IsUploaded, UploadTime, MD5Hash, IsDeleted FROM FileInfos WHERE Id = @Id";
            
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
                    IsUploaded = reader.GetInt32(8) == 1,
                    UploadTime = reader.IsDBNull(9) ? (DateTime?)null : DateTime.Parse(reader.GetString(9)),
                    MD5Hash = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    IsDeleted = reader.GetInt32(11) == 1
                };
            }
            
            return null;
        }
    }
}