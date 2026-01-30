using System;
using System.Collections.Concurrent;
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
        private readonly FileUploadService _uploadService;
        private readonly UsnJournalService _usnService = new UsnJournalService();

        public DataImportService(DatabaseContext databaseContext, FileUploadService uploadService)
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
            
            /// <summary>
            /// 文件过滤规则，如果提供则使用此规则进行额外的过滤
            /// </summary>
            public FileFilterRule? FilterRule { get; set; } = null;

            /// <summary>
            /// 是否在大规模导入时计算MD5（关闭可极大提升导入速度）
            /// </summary>
            public bool CalculateMD5 { get; set; } = true;
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
        /// 利用 USN 日志加速数据导入（快速筛选切入点）
        /// </summary>
        public async Task<ImportResult> ImportDataWithUsnAsync(string rootDirectory, ImportCriteria criteria)
        {
            // 1. 检查根目录所在的驱动器是否支持 USN
            string drive = Path.GetPathRoot(rootDirectory) ?? "C:\\";
            if (!_usnService.IsNtfsUsnSupported(drive))
            {
                Console.WriteLine($"[回退] {drive} 不支持 USN，执行全量导入...");
                return await ImportDataAsync(rootDirectory, criteria);
            }

            // 2. 从数据库获取该目录上次记录的 USN
            long lastUsn = _databaseContext.GetLastUsn(rootDirectory);
            long currentUsn = _usnService.GetCurrentUsn(drive);

            if (lastUsn == 0)
            {
                Console.WriteLine($"[初始化] {rootDirectory} 首次运行，记录当前 USN ({currentUsn}) 并执行全量扫描...");
                var initResult = await ImportDataAsync(rootDirectory, criteria);
                _databaseContext.UpdateLastUsn(rootDirectory, currentUsn); // 记录初始 USN
                return initResult;
            }

            // 3. 获取变更文件集
            HashSet<string> changedFiles = _usnService.GetModifiedFiles(drive, lastUsn);
            Console.WriteLine($"[USN] 检测到 {rootDirectory} 目录关联的 {drive} 上有 {changedFiles.Count} 个文件变更记录。");

            // 4. 执行基于 USN 快速筛选的导入
            var result = await ImportDataAsync(rootDirectory, criteria, true, FileRecord.Config.AppConfig.DefaultBatchSize, changedFiles);
            
            // 5. 导入完成后自动更新该目录的 USN 状态
            _databaseContext.UpdateLastUsn(rootDirectory, currentUsn);
            Console.WriteLine($"[完成] 已更新 {rootDirectory} 的 USN 到 {currentUsn}");
            
            return result;
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
            var batchSize = 1000; // 提升批次大小以平衡性能与稳定性
            
            int totalScanned = 0;
            int filesImported = 0;
            int filesSkipped = 0;
            int filesFailed = 0;

            if (!Directory.Exists(rootDirectory))
            {
                result.ErrorMessages.Add($"目录不存在: {rootDirectory}");
                return result;
            }

            Console.WriteLine($"[开始] 海量数据流式导入，根目录: {rootDirectory}");
            
            var searchOption = criteria.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            
            // 使用 EnumerateFiles 避免一次性将百万路径加载到内存
            var fileEnum = Directory.EnumerateFiles(rootDirectory, "*", searchOption);
            
            var batchFileInfos = new List<FileInfoModel>();
            var lockObject = new object();

            // 使用限制并发度的并行处理，防止 IO 锁死
            await Parallel.ForEachAsync(fileEnum, new ParallelOptions 
            { 
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) 
            }, async (filePath, ct) =>
            {
                Interlocked.Increment(ref totalScanned);

                try
                {
                    var fileInfo = new FileInfo(filePath);

                    // 1. 快速筛选
                    if (!IsFileMatchCriteria(fileInfo, criteria))
                    {
                        Interlocked.Increment(ref filesSkipped);
                        return;
                    }

                    // 2. 创建模型
                    var fileInfoModel = new FileInfoModel(filePath)
                    {
                        MonitorGroupId = criteria.MonitorGroupId,
                        IsDeleted = false,
                        IsUploaded = false
                    };

                    // 3. 按需计算MD5
                    if (criteria.CalculateMD5)
                    {
                        try
                        {
                            // 使用 Task.Run 确保计算逻辑不阻塞枚举主线程
                            fileInfoModel.MD5Hash = await Task.Run(() => FileUtils.CalculateMD5(filePath));
                        }
                        catch { fileInfoModel.MD5Hash = string.Empty; }
                    }

                    // 4. 线程安全的批次收集
                    List<FileInfoModel>? batchToInsert = null;
                    lock (lockObject)
                    {
                        batchFileInfos.Add(fileInfoModel);
                        if (batchFileInfos.Count >= batchSize)
                        {
                            batchToInsert = batchFileInfos;
                            batchFileInfos = new List<FileInfoModel>();
                        }
                    }

                    // 5. 触发数据库写入（在锁外执行）
                    if (batchToInsert != null)
                    {
                        _databaseContext.BatchInsertFileInfos(batchToInsert);
                        Interlocked.Add(ref filesImported, batchToInsert.Count);
                        
                        if (filesImported % 5000 == 0)
                        {
                            Console.WriteLine($"[进度] 已成功导入 {filesImported} 个文件...");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref filesFailed);
                    lock (lockObject) { result.ErrorMessages.Add($"处理失败 {filePath}: {ex.Message}"); }
                }
            });

            // 处理剩余数据
            if (batchFileInfos.Count > 0)
            {
                _databaseContext.BatchInsertFileInfos(batchFileInfos);
                Interlocked.Add(ref filesImported, batchFileInfos.Count);
            }

            result.TotalFilesScanned = totalScanned;
            result.FilesImported = filesImported;
            result.FilesSkipped = filesSkipped;
            result.FilesFailed = filesFailed;

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
        /// 根据指定条件导入文件数据（优化版本）
        /// </summary>
        /// <param name="rootDirectory">根目录路径</param>
        /// <param name="criteria">导入条件</param>
        /// <param name="parallelProcessing">是否启用并行处理</param>
        /// <param name="batchSize">批量处理大小</param>
        /// <param name="changedFiles">可选的 USN 变更文件集</param>
        /// <returns>导入结果</returns>
        public async Task<ImportResult> ImportDataAsync(string rootDirectory, ImportCriteria criteria, bool parallelProcessing = true, int batchSize = FileRecord.Config.AppConfig.DefaultBatchSize, HashSet<string>? changedFiles = null)
        {
            var result = new ImportResult();
            
            if (!Directory.Exists(rootDirectory))
            {
                result.ErrorMessages.Add($"目录不存在: {rootDirectory}");
                return result;
            }

            Console.WriteLine($"开始导入数据，根目录: {rootDirectory}");
            
            var searchOption = criteria.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allFiles = Directory.EnumerateFiles(rootDirectory, "*", searchOption);

            // 转换为数组以获取计数并传递给预筛选方法
            var fileArray = allFiles.ToArray();
            Console.WriteLine($"扫描到 {fileArray.Length} 个文件，开始筛选...");

            // 预筛选：根据文件名快速筛选，并结合 USN 变更集进行二次过滤
            var filteredFiles = PreFilterFiles(fileArray, criteria, changedFiles);
            
            Console.WriteLine($"预筛选后剩余 {filteredFiles.Count} 个文件");
            
            if (filteredFiles.Count == 0)
            {
                Console.WriteLine("没有符合条件的文件，导入完成。");
                return result;
            }
            
            // 根据是否启用并行处理来决定处理方式
            if (parallelProcessing)
            {
                await ProcessFilesInParallel(filteredFiles, criteria, result, batchSize);
            }
            else
            {
                await ProcessFilesSequentially(filteredFiles, criteria, result, batchSize);
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
        /// 预筛选文件，根据文件名和扩展名快速过滤
        /// </summary>
        /// <param name="allFiles">所有文件路径</param>
        /// <param name="criteria">筛选条件</param>
        /// <param name="changedFiles">可选的 USN 变更集</param>
        /// <returns>预筛选后的文件列表</returns>
        private List<string> PreFilterFiles(string[] allFiles, ImportCriteria criteria, HashSet<string>? changedFiles = null)
        {
            var filteredFiles = new List<string>();
            
            foreach (var filePath in allFiles)
            {
                string fileName = Path.GetFileName(filePath);

                // USN 快速筛选逻辑：
                // 如果提供了变更集，且当前文件不在变更集中，且数据库中已经存在该路径
                // 则说明该文件物理上未发生任何变化，可以直接跳过
                if (changedFiles != null && !changedFiles.Contains(fileName))
                {
                    if (_databaseContext.FileExists(filePath))
                    {
                        continue; 
                    }
                }

                // 首先检查扩展名，这是最快的筛选条件
                if (criteria.AllowedExtensions.Any())
                {
                    string extension = Path.GetExtension(filePath);
                    if (!criteria.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    {
                        continue; // 扩展名不符合，跳过
                    }
                }
                
                // 检查文件名通配符模式
                if (criteria.FileNamePatterns.Any())
                {
                    bool nameMatch = false;
                    
                    foreach (var pattern in criteria.FileNamePatterns)
                    {
                        if (IsNameMatchPattern(fileName, pattern))
                        {
                            nameMatch = true;
                            break;
                        }
                    }
                    
                    if (!nameMatch)
                    {
                        continue; // 文件名不符合模式，跳过
                    }
                }
                
                // 如果临时文件过滤启用且是临时文件，跳过
                if (criteria.SkipTemporaryFiles && FileUtils.IsTemporaryFile(filePath))
                {
                    continue;
                }
                
                // 检查FileFilterRule过滤规则
                if (criteria.FilterRule != null)
                {
                    var fileInfo = new FileInfo(filePath);
                    if (!criteria.FilterRule.IsFileAllowed(filePath, fileInfo.Length))
                    {
                        continue; // 不符合过滤规则，跳过
                    }
                }
                
                // 如果通过了快速筛选条件，则添加到待处理列表
                filteredFiles.Add(filePath);
            }
            
            return filteredFiles;
        }

        /// <summary>
        /// 并行处理文件
        /// </summary>
        /// <param name="filteredFiles">预筛选后的文件列表</param>
        /// <param name="criteria">导入条件</param>
        /// <param name="result">导入结果</param>
        /// <param name="batchSize">批量处理大小</param>
        private async Task ProcessFilesInParallel(List<string> filteredFiles, ImportCriteria criteria, ImportResult result, int batchSize)
        {
            var allFileInfoModels = new ConcurrentBag<FileInfoModel>();
            var skippedFiles = new ConcurrentBag<string>();
            var failedFiles = new ConcurrentBag<(string filePath, string errorMessage)>();
            
            // 并行处理文件，计算MD5等信息
            await Parallel.ForEachAsync(filteredFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, 
                async (filePath, ct) =>
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        
                        // 再次检查详细条件，因为预筛选只检查了部分条件
                        if (!PassesDetailedChecks(fileInfo, criteria))
                        {
                            skippedFiles.Add(filePath);
                            return;
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
                            fileInfoModel.MD5Hash = await Task.Run(() => FileUtils.CalculateMD5(filePath));
                        }
                        catch (Exception md5Ex)
                        {
                            Console.WriteLine($"计算MD5失败 {filePath}: {md5Ex.Message}");
                            fileInfoModel.MD5Hash = string.Empty;
                        }

                        allFileInfoModels.Add(fileInfoModel);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        failedFiles.Add((filePath, $"无权限访问文件: {filePath}, 错误: {ex.Message}"));
                    }
                    catch (IOException ex)
                    {
                        failedFiles.Add((filePath, $"文件I/O错误 {filePath}: {ex.Message}"));
                    }
                    catch (Exception ex)
                    {
                        failedFiles.Add((filePath, $"处理文件失败 {filePath}: {ex.Message}"));
                    }
                });

            // 更新结果统计
            result.TotalFilesScanned = filteredFiles.Count;
            result.FilesImported = allFileInfoModels.Count;
            result.FilesSkipped = skippedFiles.Count;
            result.FilesFailed = failedFiles.Count;
            
            // 添加错误信息
            foreach (var (filePath, errorMessage) in failedFiles)
            {
                result.ErrorMessages.Add(errorMessage);
                Console.WriteLine(errorMessage);
            }
            
            // 批量插入数据库
            var fileInfoList = allFileInfoModels.ToList();
            if (fileInfoList.Any())
            {
                await BatchInsertToFileDatabaseAsync(fileInfoList, batchSize);
            }
        }

        /// <summary>
        /// 顺序处理文件
        /// </summary>
        /// <param name="filteredFiles">预筛选后的文件列表</param>
        /// <param name="criteria">导入条件</param>
        /// <param name="result">导入结果</param>
        /// <param name="batchSize">批量处理大小</param>
        private async Task ProcessFilesSequentially(List<string> filteredFiles, ImportCriteria criteria, ImportResult result, int batchSize)
        {
            var batch = new List<FileInfoModel>();
            
            foreach (var filePath in filteredFiles)
            {
                result.TotalFilesScanned++;

                try
                {
                    var fileInfo = new FileInfo(filePath);

                    // 再次检查详细条件
                    if (!PassesDetailedChecks(fileInfo, criteria))
                    {
                        result.FilesSkipped++;
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

                    batch.Add(fileInfoModel);
                    result.FilesImported++;

                    // 当批次达到指定大小时，批量插入数据库
                    if (batch.Count >= batchSize)
                    {
                        await BatchInsertToFileDatabaseAsync(batch, batchSize);
                        batch.Clear();
                        
                        if (result.FilesImported % FileRecord.Config.AppConfig.ProgressReportInterval == 0)
                        {
                            Console.WriteLine($"已批量处理 {result.FilesImported} 个文件...");
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    result.FilesFailed++;
                    result.ErrorMessages.Add($"无权限访问文件: {filePath}, 错误: {ex.Message}");
                    Console.WriteLine($"无权限访问文件: {filePath}, 错误: {ex.Message}");
                }
                catch (IOException ex)
                {
                    result.FilesFailed++;
                    result.ErrorMessages.Add($"文件I/O错误 {filePath}: {ex.Message}");
                    Console.WriteLine($"文件I/O错误 {filePath}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    result.FilesFailed++;
                    result.ErrorMessages.Add($"处理文件失败 {filePath}: {ex.Message}");
                    Console.WriteLine($"处理文件失败 {filePath}: {ex.Message}");
                }
            }

            // 插入剩余的文件
            if (batch.Any())
            {
                await BatchInsertToFileDatabaseAsync(batch, batchSize);
            }
        }

        /// <summary>
        /// 批量插入文件信息到数据库
        /// </summary>
        /// <param name="fileInfoList">文件信息列表</param>
        /// <param name="batchSize">批量大小</param>
        private void BatchInsertToFileDatabase(List<FileInfoModel> fileInfoList, int batchSize)
        {
            if (!fileInfoList.Any()) return;
            
            for (int i = 0; i < fileInfoList.Count; i += batchSize)
            {
                var batch = fileInfoList.Skip(i).Take(batchSize).ToList();
                
                // 使用数据库的批量插入功能
                _databaseContext.BatchInsertFileInfos(batch);
            }
        }

        /// <summary>
        /// 检查文件是否通过详细检查（除了预筛选的条件之外）
        /// </summary>
        /// <param name="fileInfo">文件信息</param>
        /// <param name="criteria">筛选条件</param>
        /// <returns>是否通过检查</returns>
        private bool PassesDetailedChecks(FileInfo fileInfo, ImportCriteria criteria)
        {
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
            
            // 检查FileFilterRule过滤规则
            if (criteria.FilterRule != null)
            {
                if (!criteria.FilterRule.IsFileAllowed(fileInfo.FullName, fileInfo.Length))
                {
                    return false;
                }
            }

            return true;
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
            
            // 检查FileFilterRule过滤规则
            if (criteria.FilterRule != null)
            {
                if (!criteria.FilterRule.IsFileAllowed(fileInfo.FullName, fileInfo.Length))
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
        
        /// <summary>
        /// 异步批量插入文件信息到数据库
        /// </summary>
        /// <param name="fileInfoList">文件信息列表</param>
        /// <param name="batchSize">批量大小</param>
        private async Task BatchInsertToFileDatabaseAsync(List<FileInfoModel> fileInfoList, int batchSize)
        {
            if (!fileInfoList.Any()) return;
            
            for (int i = 0; i < fileInfoList.Count; i += batchSize)
            {
                var batch = fileInfoList.Skip(i).Take(batchSize).ToList();
                
                // 使用数据库的批量插入功能
                _databaseContext.BatchInsertFileInfos(batch);
                
                // 在批次之间短暂等待，避免过度占用数据库资源
                await Task.Delay(1);
            }
        }
    }
}