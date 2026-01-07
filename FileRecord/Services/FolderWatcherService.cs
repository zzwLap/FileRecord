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

            // ????????
            _watcher.Created += OnFileChanged;
            _watcher.Changed += OnFileChanged;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;

            Console.WriteLine($"???????: {_folderPath}");
            Console.WriteLine("? 'q' ?????...");
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
                    // ???????????????????
                    Thread.Sleep(100);
                    
                    if (File.Exists(e.FullPath))
                    {
                        var fileInfo = new FileInfoModel(e.FullPath);
                        _databaseContext.InsertFileInfo(fileInfo);
                        Console.WriteLine($"?????: {e.Name} ({e.ChangeType})");
                        
                        // ??????
                        _uploadService.UploadNewOrModifiedFile(e.FullPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"?????????: {ex.Message}");
            }
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                _databaseContext.DeleteFileInfo(e.FullPath);
                Console.WriteLine($"???????: {e.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"?????????: {ex.Message}");
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                // ???????
                _databaseContext.DeleteFileInfo(e.OldFullPath);
                
                // ?????????????
                if (File.Exists(e.FullPath))
                {
                    var fileInfo = new FileInfoModel(e.FullPath);
                    _databaseContext.InsertFileInfo(fileInfo);
                    Console.WriteLine($"?????????: {e.OldName} -> {e.Name}");
                    
                    // ??????
                    _uploadService.UploadNewOrModifiedFile(e.FullPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"??????????: {ex.Message}");
            }
        }

        public void ProcessExistingFiles()
        {
            Console.WriteLine("????????...");
            
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
                    Console.WriteLine($"?????? {filePath} ???: {ex.Message}");
                }
            }
            
            Console.WriteLine($"??? {allFiles.Length} ?????");
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