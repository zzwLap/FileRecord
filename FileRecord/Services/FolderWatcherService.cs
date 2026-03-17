using System;
using System.IO;
using System.Threading;
using FileRecord.Data;
using FileRecord.Models;
using FileRecord.Services.Upload;
using FileRecord.Utils;

namespace FileRecord.Services
{
    public class FolderWatcherService : IDisposable
    {
        private FileSystemWatcher? _watcher;
        private readonly DatabaseContext _databaseContext;
        private readonly FileUploadService _uploadService;
        private readonly string _folderPath;
        private readonly string _monitorGroupId;
        private readonly FileFilterRule? _filterRule;
        private bool _disposed = false;
        private DateTime _lastEventTime = DateTime.Now;
        private bool _watcherErrorOccurred = false;
        private string? _lastErrorMessage = null;

        public FolderWatcherService(string folderPath, DatabaseContext databaseContext, FileUploadService uploadService, string monitorGroupId = "default", FileFilterRule? filterRule = null)
        {
            _folderPath = folderPath;
            _databaseContext = databaseContext;
            _uploadService = uploadService;
            _monitorGroupId = monitorGroupId;
            _filterRule = filterRule;
        }

        /// <summary>
        /// 获取监控的文件夹路径
        /// </summary>
        public string FolderPath => _folderPath;

        /// <summary>
        /// 获取监控组ID
        /// </summary>
        public string MonitorGroupId => _monitorGroupId;

        /// <summary>
        /// 获取过滤规则
        /// </summary>
        public FileFilterRule? FilterRule => _filterRule;

        /// <summary>
        /// 检查监听器是否健康运行（仅检测FileSystemWatcher本身是否失效）
        /// </summary>
        public bool IsHealthy()
        {
            if (_disposed)
                return false;

            if (_watcher == null)
                return false;

            // 只有当FileSystemWatcher本身发生错误时才认为不健康
            if (_watcherErrorOccurred)
            {
                Console.WriteLine($"监听器 {_folderPath} 的FileSystemWatcher发生错误，需要重启。错误: {_lastErrorMessage}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 标记FileSystemWatcher发生错误
        /// </summary>
        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _watcherErrorOccurred = true;
            _lastErrorMessage = e.GetException()?.Message;
            Console.WriteLine($"FileSystemWatcher错误 [{_folderPath}]: {_lastErrorMessage}");
        }

        /// <summary>
        /// 重置错误状态（用于重启后）
        /// </summary>
        public void ResetErrorState()
        {
            _watcherErrorOccurred = false;
            _lastErrorMessage = null;
        }

        /// <summary>
        /// 获取文件信息（从数据库查询）
        /// </summary>
        private FileInfoModel? GetFileInfoFromDb(string filePath)
        {
            return _databaseContext.GetFileInfoByPath(filePath);
        }

        public void StartWatching()
        {
            if (!Directory.Exists(_folderPath))
            {
                throw new DirectoryNotFoundException($"Directory does not exist: {_folderPath}");
            }

            _watcher = new FileSystemWatcher(_folderPath);
            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;

            // 监听所有文件变化
            _watcher.Created += OnFileChanged;
            _watcher.Changed += OnFileChanged;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
            
            // 监听FileSystemWatcher本身的错误事件
            _watcher.Error += OnWatcherError;

            Console.WriteLine($"开始监听文件夹: {_folderPath}");
            Console.WriteLine("按'q' 键退出程序..");
        }

        public void StopWatching()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                _lastEventTime = DateTime.Now;
                
                if (e.ChangeType != WatcherChangeTypes.Created && e.ChangeType != WatcherChangeTypes.Changed)
                    return;

                // 等待文件操作完成，避免文件被占用的问题
                Thread.Sleep(FileRecord.Config.AppConfig.FileOperationWaitTimeMs);
                
                if (!File.Exists(e.FullPath))
                    return;

                var fileInfoForCheck = new FileInfo(e.FullPath);
                
                // 使用通用方法检查文件是否应该被处理
                if (!FileProcessor.ShouldProcessFile(e.FullPath, _filterRule, fileInfoForCheck.Length))
                    return;
                
                // 使用通用方法创建文件信息模型
                var fileInfo = FileProcessor.CreateFileInfoModel(e.FullPath, _monitorGroupId);
                
                // 如果文件已存在数据库中，检查是否真的有变化
                var existingFile = GetFileInfoFromDb(e.FullPath);
                if (existingFile != null && existingFile.IsDeleted)
                {
                    // 文件之前被标记为删除，现在重新创建
                    fileInfo.IsDeleted = false; // 重置删除标记
                    Console.WriteLine($"文件重新创建: {e.Name}");
                }
                
                _databaseContext.InsertFileInfo(fileInfo);
                Console.WriteLine($"文件已记录 {e.Name} ({e.ChangeType})");
                
                // 触发上传服务
                _uploadService.EnqueueNewOrModifiedFile(e.FullPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理文件变化时出错 {ex.Message}");
            }
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                _lastEventTime = DateTime.Now;
                
                // 不删除记录，而是设置删除标记
                var existingFile = GetFileInfoFromDb(e.FullPath);
                if (existingFile != null)
                {
                    existingFile.IsDeleted = true;
                    _databaseContext.InsertFileInfo(existingFile); // 更新记录
                    Console.WriteLine($"文件已标记为删除: {e.Name}");
                }
                else
                {
                    Console.WriteLine($"删除事件，但文件未在数据库中: {e.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理文件删除时出错 {ex.Message}");
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                _lastEventTime = DateTime.Now;
                
                var fileInfoForCheck = new FileInfo(e.FullPath);
                
                // 使用通用方法检查文件是否应该被处理
                if (!FileProcessor.ShouldProcessFile(e.FullPath, _filterRule, fileInfoForCheck.Length))
                {
                    Console.WriteLine($"跳过不符合过滤规则的文件重命名: {e.Name}");
                    return;
                }
                
                // 标记旧文件记录为删除状态
                var oldFile = GetFileInfoFromDb(e.OldFullPath);
                if (oldFile != null)
                {
                    oldFile.IsDeleted = true;
                    _databaseContext.InsertFileInfo(oldFile); // 更新旧文件为删除状态
                    Console.WriteLine($"旧文件已标记为删除 {e.OldName}");
                }
                
                // 如果新文件存在，添加新记录
                if (File.Exists(e.FullPath))
                {
                    // 使用通用方法创建文件信息模型
                    var fileInfo = FileProcessor.CreateFileInfoModel(e.FullPath, _monitorGroupId);
                    
                    _databaseContext.InsertFileInfo(fileInfo);
                    Console.WriteLine($"文件已重命名并记录 {e.OldName} -> {e.Name}");
                    
                    // 触发上传服务
                    _uploadService.EnqueueNewOrModifiedFile(e.FullPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理文件重命名时出错: {ex.Message}");
            }
        }

        public void ProcessExistingFiles()
        {
            Console.WriteLine("正在处理现有文件...");
            
            // 使用通用方法处理现有文件
            int processedCount = FileProcessor.ProcessExistingFiles(
                _folderPath, 
                _monitorGroupId, 
                _filterRule,
                fileInfoModel => _databaseContext.InsertFileInfo(fileInfoModel)
            );
            
            Console.WriteLine($"已处理 {processedCount} 个现有文件");
        }


        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                StopWatching();
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}



