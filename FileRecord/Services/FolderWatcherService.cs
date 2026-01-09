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

        public FolderWatcherService(string folderPath, DatabaseContext databaseContext, FileUploadService uploadService, string monitorGroupId = "default", FileFilterRule? filterRule = null)
        {
            _folderPath = folderPath;
            _databaseContext = databaseContext;
            _uploadService = uploadService;
            _monitorGroupId = monitorGroupId;
            _filterRule = filterRule;
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
                if (e.ChangeType == WatcherChangeTypes.Created || e.ChangeType == WatcherChangeTypes.Changed)
                {
                    // 等待文件操作完成，避免文件被占用的问题
                    Thread.Sleep(FileRecord.Config.AppConfig.FileOperationWaitTimeMs);
                    
                    if (File.Exists(e.FullPath))
                    {
                        // 检查文件是否符合过滤规则
                        var fileInfoForCheck = new FileInfo(e.FullPath);
                        if (_filterRule != null && !_filterRule.IsFileAllowed(e.FullPath, fileInfoForCheck.Length))
                        {
                            Console.WriteLine($"跳过不符合过滤规则的文件: {e.Name}");
                            return;
                        }

                        // 检查是否为临时文件，如果是则跳过
                        if (FileUtils.IsTemporaryFile(e.FullPath))
                        {
                            Console.WriteLine($"跳过临时文件: {e.Name}");
                            return;
                        }
                        
                        var fileInfo = new FileInfoModel(e.FullPath);
                        
                        // 设置监控组ID
                        fileInfo.MonitorGroupId = _monitorGroupId;
                        
                        // 计算MD5值
                        try
                        {
                            fileInfo.MD5Hash = FileUtils.CalculateMD5(e.FullPath);
                        }
                        catch (Exception md5Ex)
                        {
                            Console.WriteLine($"计算MD5失败 {e.Name}: {md5Ex.Message}");
                            fileInfo.MD5Hash = string.Empty;
                        }
                        
                        // 如果文件已存在数据库中，检查是否真的有变化
                        var existingFile = GetFileInfoByPath(e.FullPath);
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
                }
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
                // 不删除记录，而是设置删除标记
                var existingFile = GetFileInfoByPath(e.FullPath);
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
                // 检查新文件是否符合过滤规则
                var fileInfoForCheck = new FileInfo(e.FullPath);
                if (_filterRule != null && !_filterRule.IsFileAllowed(e.FullPath, fileInfoForCheck.Length))
                {
                    Console.WriteLine($"跳过不符合过滤规则的文件重命名: {e.Name}");
                    return;
                }
                
                // 检查新文件是否为临时文件
                if (FileUtils.IsTemporaryFile(e.FullPath))
                {
                    Console.WriteLine($"跳过临时文件重命名: {e.Name}");
                    return;
                }
                
                // 标记旧文件记录为删除状态
                var oldFile = GetFileInfoByPath(e.OldFullPath);
                if (oldFile != null)
                {
                    oldFile.IsDeleted = true;
                    _databaseContext.InsertFileInfo(oldFile); // 更新旧文件为删除状态
                    Console.WriteLine($"旧文件已标记为删除 {e.OldName}");
                }
                
                // 如果新文件存在，添加新记录
                if (File.Exists(e.FullPath))
                {
                    var fileInfo = new FileInfoModel(e.FullPath);
                    
                    // 设置监控组ID
                    fileInfo.MonitorGroupId = _monitorGroupId;
                    
                    // 计算MD5值
                    try
                    {
                        fileInfo.MD5Hash = FileUtils.CalculateMD5(e.FullPath);
                    }
                    catch (Exception md5Ex)
                    {
                        Console.WriteLine($"计算MD5失败 {e.Name}: {md5Ex.Message}");
                        fileInfo.MD5Hash = string.Empty;
                    }
                    
                    // 重置删除标记（如果文件之前被删除后重命名）
                    fileInfo.IsDeleted = false;
                    
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
            
            var allFiles = Directory.GetFiles(_folderPath, "*", SearchOption.AllDirectories);
            int processedCount = 0;
            
            foreach (var filePath in allFiles)
            {
                try
                {
                    // 检查文件是否符合过滤规则
                    var fileInfoForCheck = new FileInfo(filePath);
                    if (_filterRule != null && !_filterRule.IsFileAllowed(filePath, fileInfoForCheck.Length))
                    {
                        Console.WriteLine($"跳过不符合过滤规则的文件: {Path.GetFileName(filePath)}");
                        continue;
                    }
                    
                    // 跳过临时文件
                    if (FileUtils.IsTemporaryFile(filePath))
                    {
                        Console.WriteLine($"跳过临时文件: {Path.GetFileName(filePath)}");
                        continue;
                    }
                    
                    var fileInfo = new FileInfoModel(filePath);
                    
                    // 设置监控组ID
                    fileInfo.MonitorGroupId = _monitorGroupId;
                    
                    // 计算MD5值
                    try
                    {
                        fileInfo.MD5Hash = FileUtils.CalculateMD5(filePath);
                    }
                    catch (Exception md5Ex)
                    {
                        Console.WriteLine($"计算MD5失败 {Path.GetFileName(filePath)}: {md5Ex.Message}");
                        fileInfo.MD5Hash = string.Empty;
                    }
                    
                    // 重置删除标记（如果是重新发现的文件）
                    fileInfo.IsDeleted = false;
                    
                    _databaseContext.InsertFileInfo(fileInfo);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理现有文件 {filePath} 时出错: {ex.Message}");
                }
            }
            
            Console.WriteLine($"已处理 {processedCount} 个现有文件");
        }

        private FileInfoModel? GetFileInfoByPath(string filePath)
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_databaseContext.GetConnectionString());
            connection.Open();
            
            var selectSql = "SELECT Id, FileName, FilePath, FileSize, CreatedTime, ModifiedTime, Extension, DirectoryPath, MonitorGroupId, IsUploaded, UploadTime, MD5Hash, IsDeleted FROM FileInfos WHERE FilePath = @FilePath";
            
            using var command = new Microsoft.Data.Sqlite.SqliteCommand(selectSql, connection);
            command.Parameters.AddWithValue("@FilePath", filePath);
            
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



