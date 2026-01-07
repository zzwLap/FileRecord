using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileRecord.Data;
using FileRecord.Models;
using FileRecord.Services.Upload;

namespace FileRecord.Services
{
    public class MultiFolderWatcherService : IDisposable
    {
        private readonly Dictionary<string, FolderWatcherService> _watchers;
        private readonly DatabaseContext _databaseContext;
        private readonly FileUploadService _uploadService;
        private bool _disposed = false;

        public MultiFolderWatcherService(
            DatabaseContext databaseContext, 
            FileUploadService uploadService)
        {
            _watchers = new Dictionary<string, FolderWatcherService>();
            _databaseContext = databaseContext;
            _uploadService = uploadService;
        }

        /// <summary>
        /// ?????????
        /// </summary>
        /// <param name="folderPath">????????</param>
        /// <param name="monitorGroupId">???ID????????????</param>
        public void AddFolderToWatch(string folderPath, string monitorGroupId)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Directory does not exist: {folderPath}");
            }

            if (_watchers.ContainsKey(folderPath))
            {
                Console.WriteLine($"Directory {folderPath} is already being watched.");
                return;
            }

            // ?????FolderWatcherService??????MonitorGroupId
            var watcher = new FolderWatcherService(folderPath, _databaseContext, _uploadService, monitorGroupId);
            _watchers[folderPath] = watcher;
        }

        /// <summary>
        /// ???????
        /// </summary>
        /// <param name="folderPath">??????????</param>
        public void RemoveFolderFromWatch(string folderPath)
        {
            if (_watchers.TryGetValue(folderPath, out var watcher))
            {
                watcher.StopWatching();
                _watchers.Remove(folderPath);
            }
        }

        /// <summary>
        /// ???????????
        /// </summary>
        public void StartWatching()
        {
            foreach (var kvp in _watchers)
            {
                var folderPath = kvp.Key;
                var watcher = kvp.Value;

                try
                {
                    // ??????
                    watcher.ProcessExistingFiles();

                    // ????
                    watcher.StartWatching();
                    
                    Console.WriteLine($"Started watching folder: {folderPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start watching {folderPath}: {ex.Message}");
                }
            }

            Console.WriteLine($"Multi-folder monitoring started. Watching {_watchers.Count} folders.");
            Console.WriteLine("Press 'q' to exit...");
        }

        /// <summary>
        /// ????????
        /// </summary>
        public void StopWatching()
        {
            foreach (var kvp in _watchers)
            {
                kvp.Value.StopWatching();
            }
        }

        /// <summary>
        /// ???????????
        /// </summary>
        public int GetWatchedFolderCount()
        {
            return _watchers.Count;
        }

        /// <summary>
        /// ???????????
        /// </summary>
        public List<string> GetWatchedFolders()
        {
            return _watchers.Keys.ToList();
        }

        /// <summary>
        /// ????????????
        /// </summary>
        /// <param name="monitorGroupId">???ID</param>
        public int GetFileCountForGroup(string monitorGroupId)
        {
            var allFiles = _databaseContext.GetAllFilesForMonitorGroup(monitorGroupId);
            return allFiles.Count;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                StopWatching();
                
                foreach (var watcher in _watchers.Values)
                {
                    watcher?.Dispose();
                }
                
                _watchers.Clear();
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