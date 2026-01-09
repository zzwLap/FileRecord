using System;
using System.IO;
using FileRecord.Data;
using FileRecord.Services;
using FileRecord.Services.Upload;
using FileRecord.Tools;
using FileRecord.Utils;
using FileRecord.Tests;

// 检查命令行参数
if (args.Length > 0 && args[0] == "--view")
{
    // 查看数据库内容
    var dbTool = new DbQueryTool();
    dbTool.PrintAllFiles();
    return;
}
else if (args.Length > 0 && args[0] == "--demo")
{
    // 运行通配符规则演示
    WildcardRuleExample.DemonstrateWildcardRules();
    return;
}
else if (args.Length > 0 && args[0] == "--test")
{
    // 运行基本过滤功能测试 - 此功能已移至单独的测试项目
    Console.WriteLine("基本过滤功能测试 - 请使用 --custom-test 参数运行自定义过滤测试");
    return;
}
else if (args.Length > 0 && args[0] == "--custom-test")
{
    // 运行自定义过滤功能测试
    FileRecord.Tests.CustomFilterTest.TestCustomFiltering();
    return;
}
else if (args.Length > 0 && args[0] == "--import")
{
    // 运行数据导入功能
    await RunDataImportAsync();
    return;
}
else if (args.Length > 0 && args[0] == "--detect-missing")
{
    // 运行缺失文件检测功能
    await RunMissingFileDetectionAsync();
    return;
}

// 创建数据库上下文
var databaseContext = new DatabaseContext();

// 创建上传任务管理器
var taskManager = new UploadTaskManager(databaseContext);

// 创建上传服务
var uploadService = new FileUploadService(databaseContext, taskManager);

// 创建多目录监听服务
using var multiWatcherService = new MultiFolderWatcherService(databaseContext, uploadService);

// 获取要监听的文件夹路径列表
Console.WriteLine("请输入要监听的文件夹路径，多个路径用分号(;)分隔: ");
string folderPathsInput = Console.ReadLine() ?? "";

if (string.IsNullOrWhiteSpace(folderPathsInput))
{
    Console.WriteLine("未输入任何路径！");
    Console.WriteLine("按任意键退出...");
    Console.ReadKey();
    return;
}

// 询问是否使用过滤规则
Console.WriteLine("是否要为监控目录设置文件过滤规则？(y/n，默认为n): ");
string? useFilterInput = Console.ReadLine();
bool useFilters = !string.IsNullOrEmpty(useFilterInput) && useFilterInput.ToLower().StartsWith("y");

FileFilterRule? defaultFilterRule = FileRecord.Utils.FileFilterRuleHelper.GetInteractiveFilterRule("是否要为监控目录设置文件过滤规则？(y/n，默认为n): ", "请选择过滤规则类型:");

// 解析路径列表
string[] folderPaths = folderPathsInput.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// 添加每个路径到监控列表
foreach (var path in folderPaths)
{
    if (Directory.Exists(path))
    {
        string monitorGroupId = $"Group_{Path.GetFileName(path)}"; // 使用目录名作为监控组ID
        multiWatcherService.AddFolderToWatch(path, monitorGroupId, defaultFilterRule);
        Console.WriteLine($"已添加监控目录: {path} (组ID: {monitorGroupId})");
    }
    else
    {
        Console.WriteLine($"路径不存在，跳过: {path}");
    }
}

if (multiWatcherService.GetWatchedFolderCount() == 0)
{
    Console.WriteLine("没有有效的监控目录！");
    Console.WriteLine("按任意键退出...");
    Console.ReadKey();
    return;
}

// 开始处理上传队列
taskManager.StartProcessing();

// 开始监听所有目录
multiWatcherService.StartWatching();

// 将所有未上传的文件加入上传队列
uploadService.EnqueueAllUnuploadedFiles();

// 等待用户输入退出命令
Console.WriteLine("程序正在运行，输入 'q' 并回车退出，输入 'view' 并回车查看数据库内容，输入 'upload' 并回车上传所有未上传文件...");
while (true)
{
    var input = Console.ReadLine();
    if (input?.ToLower() == "q")
    {
        Console.WriteLine("正在退出程序...");
        break;
    }
    else if (input?.ToLower() == "view")
    {
        var dbTool = new DbQueryTool();
        dbTool.PrintAllFiles();
        Console.WriteLine("程序正在运行，输入 'q' 并回车退出，输入 'view' 并回车查看数据库内容，输入 'upload' 并回车上传所有未上传文件...");
    }
    else if (input?.ToLower() == "upload")
    {
        uploadService.EnqueueAllUnuploadedFiles();
        Console.WriteLine("程序正在运行，输入 'q' 并回车退出，输入 'view' 并回车查看数据库内容，输入 'upload' 并回车上传所有未上传文件...");
    }
}

// 停止上传任务处理
taskManager.StopProcessing();

// 数据导入功能实现
static async Task RunDataImportAsync()
{
    var databaseContext = new DatabaseContext();
    var taskManager = new UploadTaskManager(databaseContext);
    var uploadService = new FileRecord.Services.Upload.FileUploadService(databaseContext, taskManager);
    var importService = new FileRecord.Services.DataImportService(databaseContext, uploadService);
    
    Console.WriteLine("=== 数据导入功能 ===");
    Console.WriteLine("请输入要导入的根目录路径: ");
    string rootDirectory = Console.ReadLine() ?? "";
    
    if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
    {
        Console.WriteLine("目录路径无效！");
        return;
    }
    
    var criteria = new FileRecord.Services.DataImportService.ImportCriteria();
    
    // 询问是否设置文件过滤规则
    FileRecord.Utils.FileFilterRule? filterRule = FileRecord.Utils.FileFilterRuleHelper.GetInteractiveFilterRule("是否要设置文件过滤规则？(y/n，默认为n): ", "请选择过滤规则类型:");
    
    if (filterRule != null)
    {
        // 将过滤规则添加到导入条件中
        criteria.FilterRule = filterRule;
    }
    

    
    // 询问是否设置文件大小范围
    Console.WriteLine("是否要设置文件大小范围？(y/n，默认为n): ");
    string? sizeFilterInput = Console.ReadLine();
    if (!string.IsNullOrEmpty(sizeFilterInput) && sizeFilterInput.ToLower().StartsWith("y"))
    {
        Console.Write("请输入最小文件大小 (KB): ");
        if (long.TryParse(Console.ReadLine(), out long minSizeKB) && minSizeKB >= 0)
        {
            criteria.MinFileSize = minSizeKB * 1024; // 转换为字节
        }
        
        Console.Write("请输入最大文件大小 (KB): ");
        if (long.TryParse(Console.ReadLine(), out long maxSizeKB) && maxSizeKB > 0)
        {
            criteria.MaxFileSize = maxSizeKB * 1024; // 转换为字节
        }
        
        Console.WriteLine($"已设置文件大小范围: {(criteria.MinFileSize?.ToString() ?? "无限制")} - {(criteria.MaxFileSize?.ToString() ?? "无限制")} 字节");
    }
    
    // 询问是否设置修改时间范围
    Console.WriteLine("是否要设置修改时间范围？(y/n，默认为n): ");
    string? timeFilterInput = Console.ReadLine();
    if (!string.IsNullOrEmpty(timeFilterInput) && timeFilterInput.ToLower().StartsWith("y"))
    {
        Console.Write("请输入最早修改时间 (yyyy-MM-dd 格式，或天数如 '30' 表示最近30天): ");
        string? timeInput = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(timeInput))
        {
            if (int.TryParse(timeInput, out int days))
            {
                // 如果输入的是数字，认为是天数
                criteria.MinModifiedTime = DateTime.Now.AddDays(-days);
            }
            else if (DateTime.TryParse(timeInput, out DateTime minTime))
            {
                criteria.MinModifiedTime = minTime;
            }
        }
        
        Console.Write("请输入最晚修改时间 (yyyy-MM-dd 格式): ");
        string? maxTimeInput = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(maxTimeInput) && DateTime.TryParse(maxTimeInput, out DateTime maxTime))
        {
            criteria.MaxModifiedTime = maxTime;
        }
        
        Console.WriteLine($"已设置修改时间范围: {(criteria.MinModifiedTime?.ToString("yyyy-MM-dd") ?? "无限制")} - {(criteria.MaxModifiedTime?.ToString("yyyy-MM-dd") ?? "无限制")}");
    }
    
    // 询问是否设置目录路径过滤
    Console.WriteLine("是否要设置目录路径过滤？(y/n，默认为n): ");
    string? pathFilterInput = Console.ReadLine();
    if (!string.IsNullOrEmpty(pathFilterInput) && pathFilterInput.ToLower().StartsWith("y"))
    {
        Console.Write("请输入允许的目录路径模式，用逗号分隔 (例如: C:\\Projects\\*, D:\\Documents\\**): ");
        string? pathInput = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(pathInput))
        {
            criteria.AllowedDirectoryPatterns = pathInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            Console.WriteLine($"已设置目录路径过滤: {string.Join(", ", criteria.AllowedDirectoryPatterns)}");
        }
    }
    
    // 询问是否设置文件名通配符过滤
    Console.WriteLine("是否要设置文件名通配符过滤？(y/n，默认为n): ");
    string? nameFilterInput = Console.ReadLine();
    if (!string.IsNullOrEmpty(nameFilterInput) && nameFilterInput.ToLower().StartsWith("y"))
    {
        Console.Write("请输入文件名通配符模式，用逗号分隔 (例如: *a.*, *.txt): ");
        string? nameInput = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(nameInput))
        {
            criteria.FileNamePatterns = nameInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            Console.WriteLine($"已设置文件名通配符过滤: {string.Join(", ", criteria.FileNamePatterns)}");
        }
    }
    
    // 设置监控组ID
    Console.Write("请输入监控组ID (默认为 'imported'): ");
    string? groupId = Console.ReadLine();
    criteria.MonitorGroupId = string.IsNullOrWhiteSpace(groupId) ? "imported" : groupId;
    
    // 开始导入
    Console.WriteLine($"\n开始导入数据到组 '{criteria.MonitorGroupId}'...");
    var result = await importService.ImportDataAsync(rootDirectory, criteria);
    
    Console.WriteLine($"\n导入完成！");
    Console.WriteLine($"总计扫描: {result.TotalFilesScanned} 个文件");
    Console.WriteLine($"成功导入: {result.FilesImported} 个文件");
    Console.WriteLine($"跳过: {result.FilesSkipped} 个文件");
    Console.WriteLine($"失败: {result.FilesFailed} 个文件");
    
    if (result.ErrorMessages.Any())
    {
        Console.WriteLine("\n错误信息:");
        foreach (var errorMsg in result.ErrorMessages)
        {
            Console.WriteLine($"  - {errorMsg}");
        }
    }
}

static async Task RunMissingFileDetectionAsync()
{
    var databaseContext = new DatabaseContext();
    var detector = new FileRecord.Tools.MissingFileDetector(databaseContext);
    
    Console.WriteLine("=== 缺失文件检测功能 ===");
    Console.WriteLine("请输入要扫描的目录路径，多个路径用分号(;)分隔: ");
    string folderPathsInput = Console.ReadLine() ?? "";
    
    if (string.IsNullOrWhiteSpace(folderPathsInput))
    {
        Console.WriteLine("目录路径不能为空！");
        return;
    }
    
    // 解析路径列表
    string[] folderPaths = folderPathsInput.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    
    // 验证路径是否存在
    var validPaths = new List<string>();
    foreach (var path in folderPaths)
    {
        if (Directory.Exists(path))
        {
            validPaths.Add(path);
        }
        else
        {
            Console.WriteLine($"警告: 路径不存在，跳过: {path}");
        }
    }
    
    if (validPaths.Count == 0)
    {
        Console.WriteLine("没有有效的目录路径！");
        return;
    }
    
    // 询问是否要应用文件过滤规则
    FileRecord.Utils.FileFilterRule? filterRule = FileRecord.Utils.FileFilterRuleHelper.GetInteractiveFilterRule("是否要应用文件过滤规则？(y/n，默认为n): ", "请选择过滤规则类型:");
    
    Console.WriteLine("选择检测模式:");
    Console.WriteLine("1. 检测当天(今天00:00至今)未记录的文件");
    Console.WriteLine("2. 检测最近24小时未记录的文件");
    Console.WriteLine("3. 检测指定时间范围内的未记录文件");
    Console.WriteLine("4. 检测所有未记录的文件");
    Console.Write("请选择 (1-4, 默认为1): ");
    
    string? modeInput = Console.ReadLine();
    List<FileRecord.Tools.UnrecordedFileInfo> missingFiles = new List<FileRecord.Tools.UnrecordedFileInfo>();
    
    switch (modeInput)
    {
        case "2":
            // 检测最近24小时
            var yesterday = DateTime.Now.AddHours(-24);
            missingFiles = detector.FindMissingFilesInTimeRange(validPaths, yesterday, DateTime.Now, true, filterRule);
            Console.WriteLine($"正在检测从 {yesterday:yyyy-MM-dd HH:mm:ss} 到现在未记录的文件...");
            break;
        
        case "3":
            // 检测指定时间范围
            Console.Write("请输入开始时间 (yyyy-MM-dd 或 yyyy-MM-dd HH:mm:ss 格式): ");
            string? startTimeStr = Console.ReadLine();
            DateTime startTime = DateTime.Now;
            if (!DateTime.TryParse(startTimeStr, out startTime))
            {
                Console.WriteLine("开始时间格式错误！");
                return;
            }
            
            Console.Write("请输入结束时间 (yyyy-MM-dd 或 yyyy-MM-dd HH:mm:ss 格式): ");
            string? endTimeStr = Console.ReadLine();
            DateTime endTime = DateTime.Now;
            if (!DateTime.TryParse(endTimeStr, out endTime))
            {
                Console.WriteLine("结束时间格式错误！");
                return;
            }
            
            missingFiles = detector.FindMissingFilesInTimeRange(validPaths, startTime, endTime, true, filterRule);
            Console.WriteLine($"正在检测从 {startTime:yyyy-MM-dd HH:mm:ss} 到 {endTime:yyyy-MM-dd HH:mm:ss} 未记录的文件...");
            break;
            
        case "4":
            // 检测所有未记录的文件
            missingFiles = detector.FindAllMissingFiles(validPaths, true, filterRule);
            Console.WriteLine("正在检测所有未记录的文件...");
            break;
            
        case "1":
        default:
            // 检测当天
            missingFiles = detector.FindMissingFilesToday(validPaths, true, filterRule);
            Console.WriteLine($"正在检测今天(从 {DateTime.Today:yyyy-MM-dd} 00:00:00)未记录的文件...");
            break;
    }
    
    // 显示结果
    detector.DisplayMissingFiles(missingFiles);
    detector.DisplayStatistics(missingFiles);
    
    // 询问是否要导入这些文件
    if (missingFiles.Count > 0)
    {
        Console.WriteLine($"\n发现 {missingFiles.Count} 个未记录的文件。是否要将它们导入到数据库？(y/n): ");
        string? importInput = Console.ReadLine();
        
        if (!string.IsNullOrEmpty(importInput) && importInput.ToLower().StartsWith("y"))
        {
            await ImportMissingFilesAsync(databaseContext, missingFiles);
        }
    }
}

static async Task ImportMissingFilesAsync(DatabaseContext databaseContext, List<FileRecord.Tools.UnrecordedFileInfo> missingFiles)
{
    var taskManager = new UploadTaskManager(databaseContext);
    var uploadService = new FileRecord.Services.Upload.FileUploadService(databaseContext, taskManager);
    var importService = new FileRecord.Services.DataImportService(databaseContext, uploadService);
    
    Console.WriteLine("\n开始导入未记录的文件...");
    
    var criteria = new FileRecord.Services.DataImportService.ImportCriteria();
    criteria.MonitorGroupId = "missing_detected"; // 使用特殊监控组标识这些是检测到的缺失文件
    
    // 将未记录的文件路径按目录分组
    var directoryGroups = missingFiles.GroupBy(f => Path.GetDirectoryName(f.FilePath)).ToList();
    
    int totalImported = 0;
    foreach (var group in directoryGroups)
    {
        var groupFiles = group.ToList();
        Console.WriteLine($"正在处理目录: {group.Key} ({groupFiles.Count} 个文件)");
        
        // 创建临时的导入服务，只处理当前目录的文件
        var tempCriteria = new FileRecord.Services.DataImportService.ImportCriteria();
        tempCriteria.MonitorGroupId = "missing_detected";
        
        // 由于ImportDataAsync方法是为整个目录设计的，我们需要为每个文件单独创建记录
        foreach (var fileInfo in groupFiles)
        {
            try
            {
                var fileInfoModel = new FileRecord.Models.FileInfoModel(fileInfo.FilePath)
                {
                    MonitorGroupId = "missing_detected",
                    IsDeleted = false,
                    IsUploaded = false,
                    UploadTime = null
                };
                
                // 计算MD5值
                try
                {
                    fileInfoModel.MD5Hash = FileRecord.Utils.FileUtils.CalculateMD5(fileInfo.FilePath);
                }
                catch (Exception md5Ex)
                {
                    Console.WriteLine($"计算MD5失败 {fileInfo.FilePath}: {md5Ex.Message}");
                    fileInfoModel.MD5Hash = string.Empty;
                }
                
                // 插入数据库
                databaseContext.InsertFileInfo(fileInfoModel);
                totalImported++;
                
                if (totalImported % 50 == 0)
                {
                    Console.WriteLine($"已导入 {totalImported} 个文件...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"导入文件失败 {fileInfo.FilePath}: {ex.Message}");
            }
        }
    }
    
    Console.WriteLine($"\n导入完成！共导入 {totalImported} 个文件。");
    
    // 将新导入的文件添加到上传队列
    Console.WriteLine("将新导入的文件添加到上传队列...");
    uploadService.EnqueueAllUnuploadedFiles();
}