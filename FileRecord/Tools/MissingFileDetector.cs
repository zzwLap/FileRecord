using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileRecord.Data;
using FileRecord.Models;
using FileRecord.Utils;

namespace FileRecord.Tools
{
    /// <summary>
    /// 缺失文件检测器，用于查找当天或指定时间范围内未被系统记录的文件
    /// </summary>
    public class MissingFileDetector
    {
        private readonly DatabaseContext _databaseContext;

        public MissingFileDetector(DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
        }

        /// <summary>
        /// 查找当天未被记录的文件
        /// </summary>
        /// <param name="directoryPaths">要扫描的目录路径列表</param>
        /// <param name="includeSubdirectories">是否包含子目录</param>
        /// <param name="filterRule">文件过滤规则，如果提供则只返回符合规则的文件</param>
        /// <returns>未被记录的文件信息列表</returns>
        public List<UnrecordedFileInfo> FindMissingFilesToday(List<string> directoryPaths, bool includeSubdirectories = true, FileFilterRule? filterRule = null)
        {
            DateTime todayStart = DateTime.Today;
            DateTime todayEnd = DateTime.Now;

            return FindMissingFilesInTimeRange(directoryPaths, todayStart, todayEnd, includeSubdirectories, filterRule);
        }

        /// <summary>
        /// 查找指定时间范围内的未记录文件
        /// </summary>
        /// <param name="directoryPaths">要扫描的目录路径列表</param>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <param name="includeSubdirectories">是否包含子目录</param>
        /// <param name="filterRule">文件过滤规则，如果提供则只返回符合规则的文件</param>
        /// <returns>未被记录的文件信息列表</returns>
        public List<UnrecordedFileInfo> FindMissingFilesInTimeRange(
            List<string> directoryPaths, 
            DateTime startTime, 
            DateTime endTime, 
            bool includeSubdirectories = true,
            FileFilterRule? filterRule = null)
        {
            var allScannedFiles = new List<FileInfo>();
            var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            // 扫描指定目录下在时间范围内的文件
            foreach (var directoryPath in directoryPaths)
            {
                if (Directory.Exists(directoryPath))
                {
                    var filesInDir = Directory.GetFiles(directoryPath, "*", searchOption)
                        .Select(f => new FileInfo(f))
                        .Where(f => f.LastWriteTime >= startTime && f.LastWriteTime <= endTime)
                        .ToList();

                    allScannedFiles.AddRange(filesInDir);
                }
            }

            // 应用过滤规则
            if (filterRule != null)
            {
                allScannedFiles = allScannedFiles
                    .Where(fileInfo => filterRule.IsFileAllowed(fileInfo.FullName, fileInfo.Length))
                    .ToList();
            }

            // 从数据库获取指定时间范围内的已记录文件路径
            var recordedFilePaths = _databaseContext.GetFileInfosInTimeRange(startTime, endTime)
                .Select(fi => fi.FilePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 找出未记录的文件
            var unrecordedFiles = allScannedFiles
                .Where(fileInfo => !recordedFilePaths.Contains(fileInfo.FullName))
                .Select(fileInfo => new UnrecordedFileInfo
                {
                    FilePath = fileInfo.FullName,
                    FileName = fileInfo.Name,
                    DirectoryPath = fileInfo.DirectoryName,
                    Size = fileInfo.Length,
                    ModifiedTime = fileInfo.LastWriteTime,
                    CreatedTime = fileInfo.CreationTime,
                    Extension = fileInfo.Extension
                })
                .ToList();

            return unrecordedFiles;
        }

        /// <summary>
        /// 查找指定目录下所有未被记录的文件（不限时间范围）
        /// </summary>
        /// <param name="directoryPaths">要扫描的目录路径列表</param>
        /// <param name="includeSubdirectories">是否包含子目录</param>
        /// <param name="filterRule">文件过滤规则，如果提供则只返回符合规则的文件</param>
        /// <returns>未被记录的文件信息列表</returns>
        public List<UnrecordedFileInfo> FindAllMissingFiles(List<string> directoryPaths, bool includeSubdirectories = true, FileFilterRule? filterRule = null)
        {
            var allScannedFiles = new List<FileInfo>();
            var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            // 扫描指定目录下的所有文件
            foreach (var directoryPath in directoryPaths)
            {
                if (Directory.Exists(directoryPath))
                {
                    var filesInDir = Directory.GetFiles(directoryPath, "*", searchOption)
                        .Select(f => new FileInfo(f))
                        .ToList();

                    allScannedFiles.AddRange(filesInDir);
                }
            }

            // 应用过滤规则
            if (filterRule != null)
            {
                allScannedFiles = allScannedFiles
                    .Where(fileInfo => filterRule.IsFileAllowed(fileInfo.FullName, fileInfo.Length))
                    .ToList();
            }

            // 从数据库获取所有已记录的文件路径
            var recordedFilePaths = _databaseContext.GetAllFilePaths()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 找出未记录的文件
            var unrecordedFiles = allScannedFiles
                .Where(fileInfo => !recordedFilePaths.Contains(fileInfo.FullName))
                .Select(fileInfo => new UnrecordedFileInfo
                {
                    FilePath = fileInfo.FullName,
                    FileName = fileInfo.Name,
                    DirectoryPath = fileInfo.DirectoryName,
                    Size = fileInfo.Length,
                    ModifiedTime = fileInfo.LastWriteTime,
                    CreatedTime = fileInfo.CreationTime,
                    Extension = fileInfo.Extension
                })
                .ToList();

            return unrecordedFiles;
        }

        /// <summary>
        /// 显示未记录文件列表
        /// </summary>
        /// <param name="unrecordedFiles">未记录文件列表</param>
        public void DisplayMissingFiles(List<UnrecordedFileInfo> unrecordedFiles)
        {
            if (unrecordedFiles == null || !unrecordedFiles.Any())
            {
                Console.WriteLine("没有发现未记录的文件。");
                return;
            }

            Console.WriteLine($"\n发现 {unrecordedFiles.Count} 个未记录的文件：");
            Console.WriteLine(new string('-', 100));
            Console.WriteLine($"{"文件名",-30} {"路径",-40} {"大小(B)",-12} {"修改时间",-20}");
            Console.WriteLine(new string('-', 100));

            foreach (var file in unrecordedFiles.Take(50)) // 只显示前50个，避免输出过多
            {
                string fileName = file.FileName.Length > 28 ? file.FileName.Substring(0, 25) + "..." : file.FileName;
                string directoryPath = file.DirectoryPath.Length > 38 ? file.DirectoryPath.Substring(0, 35) + "..." : file.DirectoryPath;
                
                Console.WriteLine($"{fileName,-30} {directoryPath,-40} {file.Size,-12} {file.ModifiedTime:yyyy-MM-dd HH:mm:ss,-20}");
            }

            if (unrecordedFiles.Count > 50)
            {
                Console.WriteLine($"... 还有 {unrecordedFiles.Count - 50} 个文件未显示");
            }

            Console.WriteLine(new string('-', 100));
        }

        /// <summary>
        /// 统计未记录文件信息
        /// </summary>
        /// <param name="unrecordedFiles">未记录文件列表</param>
        public void DisplayStatistics(List<UnrecordedFileInfo> unrecordedFiles)
        {
            if (unrecordedFiles == null || !unrecordedFiles.Any())
            {
                Console.WriteLine("\n统计信息：未发现未记录的文件");
                return;
            }

            var totalSize = unrecordedFiles.Sum(f => f.Size);
            var earliestTime = unrecordedFiles.Min(f => f.ModifiedTime);
            var latestTime = unrecordedFiles.Max(f => f.ModifiedTime);
            var extensionGroups = unrecordedFiles.GroupBy(f => f.Extension).OrderByDescending(g => g.Count());

            Console.WriteLine("\n统计信息：");
            Console.WriteLine($"总文件数: {unrecordedFiles.Count}");
            Console.WriteLine($"总大小: {FormatFileSize(totalSize)}");
            Console.WriteLine($"时间范围: {earliestTime:yyyy-MM-dd HH:mm:ss} - {latestTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine("文件类型分布:");
            
            foreach (var group in extensionGroups.Take(10)) // 显示前10种类型的分布
            {
                Console.WriteLine($"  {group.Key ?? "无扩展名"}: {group.Count()} 个文件");
            }
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        /// <param name="bytes">字节数</param>
        /// <returns>格式化的大小字符串</returns>
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// 未记录文件信息类
    /// </summary>
    public class UnrecordedFileInfo
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string DirectoryPath { get; set; }
        public long Size { get; set; }
        public DateTime ModifiedTime { get; set; }
        public DateTime CreatedTime { get; set; }
        public string Extension { get; set; }
    }
}