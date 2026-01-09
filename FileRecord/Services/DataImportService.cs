using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileRecord.Data;
using FileRecord.Models;
using FileRecord.Services.Upload;
using FileRecord.Utils;
using System.Text.RegularExpressions;

namespace FileRecord.Services
{
    /// <summary>
    /// 数据导入服务类，用于根据筛选条件从文件系统导入历史文件信息到数据库
    /// </summary>
    public class DataImportService
    {
        private readonly DatabaseContext _databaseContext;
        private readonly FileRecord.Services.Upload.FileUploadService _uploadService;

        public DataImportService(DatabaseContext databaseContext, FileRecord.Services.Upload.FileUploadService uploadService)
        {
            _databaseContext = databaseContext;
            _uploadService = uploadService;
        }

        /// <summary>
        /// 导入条件类，定义筛选条件
        /// </summary>
        public class ImportCriteria
        {
            /// <summary>
            /// 允许的文件扩展名列表，如 [".cs", ".txt"]
            /// </summary>
            public List<string> AllowedExtensions { get; set; } = new List<string>();

            /// <summary>
            /// 最小文件大小（字节），null表示无限制
            /// </summary>
            public long? MinFileSize { get; set; } = null;

            /// <summary>
            /// 最大文件大小（字节），null表示无限制
            /// </summary>
            public long? MaxFileSize { get; set; } = null;

            /// <summary>
            /// 最小修改时间，null表示无限制
            /// </summary>
            public DateTime? MinModifiedTime { get; set; } = null;

            /// <summary>
            /// 最大修改时间，null表示无限制
            /// </summary>
            public DateTime? MaxModifiedTime { get; set; } = null;

            /// <summary>
            /// 允许的目录路径列表，支持通配符，如 ["C:\\Projects\\*", "D:\\Documents\\**"]
            /// </summary>
            public List<string> AllowedDirectoryPatterns { get; set; } = new List<string>();

            /// <summary>
            /// 文件名通配符模式，如 ["*a.*", "*.txt"]
            /// </summary>
            public List<string> FileNamePatterns { get; set; } = new List<string>();

            /// <summary>
            /// 监控组ID
            /// </summary>
            public string MonitorGroupId { get; set; } = "default";

            /// <summary>
            /// 是否包含子目录
            /// </summary>
            public bool IncludeSubdirectories { get; set; } = true;

            /// <summary>
            /// 是否跳过临时文件
            /// </summary>
            public bool SkipTemporaryFiles { get; set; } = true;
        }

        /// <summary>
        /// 导入结果类
        /// </summary>
        public class ImportResult
        {
            public int TotalFilesScanned { get; set; }
            public int FilesImported { get; set; }
            public int FilesSkipped { get; set; }
            public int FilesFailed { get; set; }
            public List<string> ErrorMessages { get; set; } = new List<string>();
        }

        /// <summary>
        /// 根据指定条件导入文件数据
        /// </summary>
        /// <param name="rootDirectory">根目录路径</param>
        /// <param name="criteria">导入条件</param>
        /// <returns>导入结果</returns>
        public async Task<ImportResult> ImportDataAsync(string rootDirectory, ImportCriteria criteria)
        {
            var result = new ImportResult();
            
            if (!Directory.Exists(rootDirectory))
            {
                result.ErrorMessages.Add($"目录不存在: {rootDirectory}");
                return result;
            }

            Console.WriteLine($"开始导入数据，根目录: {rootDirectory}");
            
            var searchOption = criteria.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allFiles = Directory.GetFiles(rootDirectory, "*", searchOption);

            Console.WriteLine($"扫描到 {allFiles.Length} 个文件，开始筛选...");

            foreach (var filePath in allFiles)
            {
                result.TotalFilesScanned++;

                try
                {
                    var fileInfo = new FileInfo(filePath);

                    // 检查是否符合筛选条件
                    if (!IsFileMatchCriteria(fileInfo, criteria))
                    {
                        result.FilesSkipped++;
                        continue;
                    }

                    // 检查是否为临时文件
                    if (criteria.SkipTemporaryFiles && FileUtils.IsTemporaryFile(filePath))
                    {
                        result.FilesSkipped++;
                        Console.WriteLine($"跳过临时文件: {Path.GetFileName(filePath)}");
                        continue;
                    }

                    // 创建数据库记录
                    var fileInfoModel = new FileInfoModel(filePath)
                    {
                        MonitorGroupId = criteria.MonitorGroupId,
                        IsDeleted = false, // 文件存在，不是删除状态
                        IsUploaded = false, // 新导入的文件默认未上传
                        UploadTime = null
                    };

                    // 计算MD5值
                    try
                    {
                        fileInfoModel.MD5Hash = FileUtils.CalculateMD5(filePath);
                    }
                    catch (Exception md5Ex)
                    {
                        Console.WriteLine($"计算MD5失败 {filePath}: {md5Ex.Message}");
                        fileInfoModel.MD5Hash = string.Empty;
                    }

                    // 插入或更新数据库记录
                    _databaseContext.InsertFileInfo(fileInfoModel);
                    result.FilesImported++;

                    if (result.FilesImported % 100 == 0)
                    {
                        Console.WriteLine($"已导入 {result.FilesImported} 个文件...");
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    result.FilesFailed++;
                    result.ErrorMessages.Add($"无权限访问文件: {filePath}");
                    Console.WriteLine($"无权限访问文件: {filePath}");
                }
                catch (Exception ex)
                {
                    result.FilesFailed++;
                    result.ErrorMessages.Add($"处理文件失败 {filePath}: {ex.Message}");
                    Console.WriteLine($"处理文件失败 {filePath}: {ex.Message}");
                }
            }

            Console.WriteLine($"导入完成。总计扫描: {result.TotalFilesScanned}, 导入: {result.FilesImported}, 跳过: {result.FilesSkipped}, 失败: {result.FilesFailed}");

            // 将新导入的文件添加到上传队列
            if (result.FilesImported > 0)
            {
                Console.WriteLine("将新导入的文件添加到上传队列...");
                _uploadService.EnqueueAllUnuploadedFiles();
            }

            return result;
        }

        /// <summary>
        /// 检查文件是否符合筛选条件
        /// </summary>
        /// <param name="fileInfo">文件信息</param>
        /// <param name="criteria">筛选条件</param>
        /// <returns>是否符合条件</returns>
        private bool IsFileMatchCriteria(FileInfo fileInfo, ImportCriteria criteria)
        {
            // 检查文件扩展名
            if (criteria.AllowedExtensions.Any() && 
                !criteria.AllowedExtensions.Contains(fileInfo.Extension, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            // 检查文件大小
            if (criteria.MinFileSize.HasValue && fileInfo.Length < criteria.MinFileSize.Value)
            {
                return false;
            }

            if (criteria.MaxFileSize.HasValue && fileInfo.Length > criteria.MaxFileSize.Value)
            {
                return false;
            }

            // 检查修改时间
            if (criteria.MinModifiedTime.HasValue && fileInfo.LastWriteTime < criteria.MinModifiedTime.Value)
            {
                return false;
            }

            if (criteria.MaxModifiedTime.HasValue && fileInfo.LastWriteTime > criteria.MaxModifiedTime.Value)
            {
                return false;
            }

            // 检查目录路径匹配
            if (criteria.AllowedDirectoryPatterns.Any())
            {
                bool pathMatch = false;
                foreach (var pattern in criteria.AllowedDirectoryPatterns)
                {
                    if (IsPathMatchPattern(fileInfo.DirectoryName, pattern))
                    {
                        pathMatch = true;
                        break;
                    }
                }
                if (!pathMatch)
                {
                    return false;
                }
            }

            // 检查文件名模式
            if (criteria.FileNamePatterns.Any())
            {
                bool nameMatch = false;
                foreach (var pattern in criteria.FileNamePatterns)
                {
                    if (IsNameMatchPattern(fileInfo.Name, pattern))
                    {
                        nameMatch = true;
                        break;
                    }
                }
                if (!nameMatch)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 检查路径是否匹配模式（支持通配符）
        /// </summary>
        /// <param name="path">实际路径</param>
        /// <param name="pattern">模式（支持*和**）</param>
        /// <returns>是否匹配</returns>
        private bool IsPathMatchPattern(string path, string pattern)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(pattern))
                return string.Equals(path, pattern, StringComparison.OrdinalIgnoreCase);

            // 处理通配符模式
            pattern = pattern.Replace(".", "\\.").Replace("?", ".").Replace("**", ".*");
            pattern = pattern.Replace("*", "[^/\\\\]*");
            pattern = "^" + pattern + "$";

            try
            {
                return Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                // 如果正则表达式无效，回退到简单的字符串比较
                return path.Contains(pattern);
            }
        }

        /// <summary>
        /// 检查文件名是否匹配模式（支持通配符）
        /// </summary>
        /// <param name="name">实际文件名</param>
        /// <param name="pattern">模式（支持*和?）</param>
        /// <returns>是否匹配</returns>
        private bool IsNameMatchPattern(string name, string pattern)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(pattern))
                return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);

            // 将通配符转换为正则表达式
            string regexPattern = ConvertWildcardToRegex(pattern);
            
            try
            {
                return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                // 如果正则表达式无效，回退到简单的字符串比较
                return name.Contains(pattern);
            }
        }

        /// <summary>
        /// 将通配符模式转换为正则表达式
        /// </summary>
        /// <param name="wildcard">通配符模式</param>
        /// <returns>正则表达式</returns>
        private string ConvertWildcardToRegex(string wildcard)
        {
            string regex = wildcard
                .Replace(".", "\\.")  // 转义点号
                .Replace("?", ".")   // ? 匹配任意单个字符
                .Replace("*", ".*"); // * 匹配任意多个字符
            
            return "^" + regex + "$"; // 完整匹配
        }
    }
}