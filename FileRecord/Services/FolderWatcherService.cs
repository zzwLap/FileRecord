using System;
using System.IO;
using System.Threading;
using FileRecord.Data;
using FileRecord.Models;
using FileRecord.Services.Upload;

namespace FileRecord.Services
{
    public class FolderWatcherService : IDisposable
    {
        private FileSystemWatcher? _watcher;
        private readonly DatabaseContext _databaseContext;
        private readonly FileUploadService _uploadService;
        private readonly string _folderPath;
        private bool _disposed = false;

        public FolderWatcherService(string folderPath, DatabaseContext databaseContext, FileUploadService uploadService)
        {
            _folderPath = folderPath;
            _databaseContext = databaseContext;
            _uploadService = uploadService;
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

            Console.WriteLine($"开始监听文件夹: {_folderPath}");
            Console.WriteLine("按 'q' 键退出程序...");
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
                if (e.ChangeType == WatcherChangeTypes.Created || e.ChangeType == WatcherChangeTypes.Changed)
                {
                    // 等待文件操作完成，避免文件被占用的问题
                    Thread.Sleep(100);
                    
                    if (File.Exists(e.FullPath))
                    {
                        var fileInfo = new FileInfoModel(e.FullPath);
                        _databaseContext.InsertFileInfo(fileInfo);
                        Console.WriteLine($"文件已记录: {e.Name} ({e.ChangeType})");
                        
                        // 触发上传服务
                        _uploadService.EnqueueNewOrModifiedFile(e.FullPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理文件变化时出错: {ex.Message}");
            }
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                _databaseContext.DeleteFileInfo(e.FullPath);
                Console.WriteLine($"文件已删除记录: {e.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理文件删除时出错: {ex.Message}");
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                // 删除旧文件记录
                _databaseContext.DeleteFileInfo(e.OldFullPath);
                
                // 如果新文件存在，添加新记录
                if (File.Exists(e.FullPath))
                {
                    var fileInfo = new FileInfoModel(e.FullPath);
                    _databaseContext.InsertFileInfo(fileInfo);
                    Console.WriteLine($"文件已重命名并记录: {e.OldName} -> {e.Name}");
                    
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
            
            var allFiles = Directory.GetFiles(_folderPath, "*", SearchOption.AllDirectories);
            foreach (var filePath in allFiles)
            {
                try
                {
                    var fileInfo = new FileInfoModel(filePath);
                    _databaseContext.InsertFileInfo(fileInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理现有文件 {filePath} 时出错: {ex.Message}");
                }
            }
            
            Console.WriteLine($"已处理 {allFiles.Length} 个现有文件");
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