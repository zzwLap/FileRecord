using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileRecord.Config;
using FileRecord.Data;
using FileRecord.Models;
using FileRecord.Services.Upload;
using FileRecord.Utils;

namespace FileRecord.Services
{
    public class MultiFolderWatcherService : IDisposable
    {
        private readonly Dictionary<string, FolderWatcherService> _watchers;
        private readonly Dictionary<string, WatcherConfig> _watcherConfigs;
        private readonly DatabaseContext _databaseContext;
        private readonly FileUploadService _uploadService;
        private bool _disposed = false;
        private Timer? _healthCheckTimer;
        private readonly TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(AppConfig.HealthCheckIntervalMinutes);

        /// <summary>
        /// 监听器配置信息，用于故障恢复时重建监听器
        /// </summary>
        private class WatcherConfig
        {
            public string FolderPath { get; set; } = string.Empty;
            public string MonitorGroupId { get; set; } = string.Empty;
            public FileFilterRule? FilterRule { get; set; }
        }

        public MultiFolderWatcherService(
            DatabaseContext databaseContext, 
            FileUploadService uploadService)
        {
            _watchers = new Dictionary<string, FolderWatcherService>();
            _watcherConfigs = new Dictionary<string, WatcherConfig>();
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

            // 保存配置信息，用于故障恢复
            _watcherConfigs[folderPath] = new WatcherConfig
            {
                FolderPath = folderPath,
                MonitorGroupId = monitorGroupId,
                FilterRule = filterRule
            };

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

            // 启动健康检查定时器
            StartHealthCheckTimer();

            Console.WriteLine($"Multi-folder monitoring started. Watching {_watchers.Count} folders.");
            Console.WriteLine("Press 'q' to exit...");
        }

        /// <summary>
        /// 启动健康检查定时器
        /// </summary>
        private void StartHealthCheckTimer()
        {
            _healthCheckTimer = new Timer(CheckWatchersHealth, null, _healthCheckInterval, _healthCheckInterval);
            Console.WriteLine($"健康检查定时器已启动，检查间隔: {_healthCheckInterval.TotalMinutes} 分钟");
        }

        /// <summary>
        /// 检查所有监听器的健康状态
        /// </summary>
        private void CheckWatchersHealth(object? state)
        {
            if (_disposed)
                return;

            var unhealthyWatchers = new List<string>();

            // 检查每个监听器的状态
            foreach (var kvp in _watchers)
            {
                var folderPath = kvp.Key;
                var watcher = kvp.Value;

                if (!watcher.IsHealthy())
                {
                    unhealthyWatchers.Add(folderPath);
                    Console.WriteLine($"检测到监听器故障: {folderPath}");
                }
            }

            // 重启不健康的监听器
            foreach (var folderPath in unhealthyWatchers)
            {
                RestartWatcher(folderPath);
            }
        }

        /// <summary>
        /// 重启指定路径的监听器
        /// </summary>
        private void RestartWatcher(string folderPath)
        {
            try
            {
                Console.WriteLine($"正在重启监听器: {folderPath}");

                // 获取配置信息
                if (!_watcherConfigs.TryGetValue(folderPath, out var config))
                {
                    Console.WriteLine($"无法找到监听器配置: {folderPath}");
                    return;
                }

                // 停止并释放旧的监听器
                if (_watchers.TryGetValue(folderPath, out var oldWatcher))
                {
                    try
                    {
                        oldWatcher.StopWatching();
                        oldWatcher.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"停止旧监听器时出错: {ex.Message}");
                    }
                }

                // 创建新的监听器
                var newWatcher = new FolderWatcherService(
                    config.FolderPath, 
                    _databaseContext, 
                    _uploadService, 
                    config.MonitorGroupId, 
                    config.FilterRule);

                // 替换旧的监听器
                _watchers[folderPath] = newWatcher;

                // 启动新的监听器
                newWatcher.ProcessExistingFiles();
                newWatcher.StartWatching();
                
                // 重置错误状态
                newWatcher.ResetErrorState();

                Console.WriteLine($"监听器已成功重启: {folderPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"重启监听器失败 {folderPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止监控所有文件夹
        /// </summary>
        public void StopWatching()
        {
            // 停止健康检查定时器
            _healthCheckTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            
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
                
                // 释放定时器
                _healthCheckTimer?.Dispose();
                _healthCheckTimer = null;
                
                foreach (var watcher in _watchers.Values)
                {
                    watcher?.Dispose();
                }
                
                _watchers.Clear();
                _watcherConfigs.Clear();
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