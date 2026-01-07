        /// <summary>
        /// 添加要监控的文件夹
        /// </summary>
        /// <param name="folderPath">要监控的文件夹路径</param>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileRecord.Data;
using FileRecord.Models;
using FileRecord.Services.Upload;
using FileRecord.Utils;

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
        /// 添加要监控的文件夹
        /// </summary>
        /// <param name="folderPath">要监控的文件夹路径</param>
        /// <param name="monitorGroupId">监控组ID，用于区分不同监控目录</param>
        /// <param name="filterRule">文件过滤规则，如果为null则不进行过滤</param>
        public void AddFolderToWatch(string folderPath, string monitorGroupId, FileFilterRule? filterRule = null)
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

            // 创建FolderWatcherService实例并设置MonitorGroupId
            var watcher = new FolderWatcherService(folderPath, _databaseContext, _uploadService, monitorGroupId, filterRule);
            _watchers[folderPath] = watcher;
        }

        /// <summary>
        /// 移除监控的文件夹
        /// </summary>
        /// <param name="folderPath">要移除监控的文件夹路径</param>
        public void RemoveFolderFromWatch(string folderPath)
        {
            if (_watchers.TryGetValue(folderPath, out var watcher))
            {
                watcher.StopWatching();
                _watchers.Remove(folderPath);
            }
        }

        /// <summary>
        /// 开始监控所有文件夹
        /// </summary>
        public void StartWatching()
        {
            foreach (var kvp in _watchers)
            {
                var folderPath = kvp.Key;
                var watcher = kvp.Value;

                try
                {
                    // 处理现有文件
                    watcher.ProcessExistingFiles();

                    // 开始监控
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
        /// 停止监控所有文件夹
        /// </summary>
        public void StopWatching()
        {
            foreach (var kvp in _watchers)
            {
                kvp.Value.StopWatching();
            }
        }

        /// <summary>
        /// 获取监控的文件夹数量
        /// </summary>
        public int GetWatchedFolderCount()
        {
            return _watchers.Count;
        }

        /// <summary>
        /// 获取所有被监控的文件夹列表
        /// </summary>
        public List<string> GetWatchedFolders()
        {
            return _watchers.Keys.ToList();
        }

        /// <summary>
        /// 获取特定监控组的文件数量
        /// </summary>
        /// <param name="monitorGroupId">监控组ID</param>
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