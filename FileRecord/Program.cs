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

FileFilterRule? defaultFilterRule = null;
if (useFilters)
{
    Console.WriteLine("选择过滤规则类型:");
    Console.WriteLine("1. 文档文件 (.doc, .pdf, .txt等)");
    Console.WriteLine("2. 图片文件 (.jpg, .png, .gif等)");
    Console.WriteLine("3. 视频文件 (.mp4, .avi, .mkv等)");
    Console.WriteLine("4. 音频文件 (.mp3, .wav, .flac等)");
    Console.WriteLine("5. 代码文件 (.cs, .js, .py等)");
    Console.WriteLine("6. 自定义扩展名");
    Console.WriteLine("7. 按文件大小过滤");
    Console.WriteLine("8. 通配符模式 (*a.*, *.txt, test* 等)");
    Console.WriteLine("9. 包含特定字符 (如包含字母 a 的文件)");
    Console.Write("请选择 (1-9, 默认为全部文件): ");
    
    string? filterChoice = Console.ReadLine();
    
    switch (filterChoice)
    {
        case "1":
            defaultFilterRule = FileFilterRuleFactory.CreateDocumentRule();
            Console.WriteLine($"已选择文档文件过滤规则");
            break;
        case "2":
            defaultFilterRule = FileFilterRuleFactory.CreateImageRule();
            Console.WriteLine($"已选择图片文件过滤规则");
            break;
        case "3":
            defaultFilterRule = FileFilterRuleFactory.CreateVideoRule();
            Console.WriteLine($"已选择视频文件过滤规则");
            break;
        case "4":
            defaultFilterRule = FileFilterRuleFactory.CreateAudioRule();
            Console.WriteLine($"已选择音频文件过滤规则");
            break;
        case "5":
            defaultFilterRule = FileFilterRuleFactory.CreateCodeRule();
            Console.WriteLine($"已选择代码文件过滤规则");
            break;
        case "6":
            Console.Write("请输入允许的扩展名，用逗号分隔 (例如: .txt,.pdf,.doc): ");
            string? customExtsInput = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(customExtsInput))
            {
                string[] customExts = customExtsInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                defaultFilterRule = FileFilterRuleFactory.CreateCustomRule(customExts);
                Console.WriteLine($"已选择自定义扩展名过滤规则");
            }
            break;
        case "7":
            Console.Write("请输入最大文件大小 (MB): ");
            if (double.TryParse(Console.ReadLine(), out double maxMb) && maxMb > 0)
            {
                long maxSize = (long)(maxMb * 1024 * 1024);
                defaultFilterRule = FileFilterRuleFactory.CreateSizeRule(0, maxSize);
                Console.WriteLine($"已选择大小限制过滤规则 (最大 {maxMb} MB)");
            }
            break;
        case "8":
            Console.Write("请输入通配符模式 (例如: *a.*, *.txt, test*, 等): ");
            string? wildcardPattern = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(wildcardPattern))
            {
                defaultFilterRule = FileFilterRuleFactory.CreateWildcardRule(wildcardPattern);
                Console.WriteLine($"已选择通配符过滤规则: {wildcardPattern}");
            }
            break;
        case "9":
            Console.Write("请输入要匹配的字符 (例如: a, b, c 等): ");
            string? charInput = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(charInput) && charInput.Length == 1)
            {
                defaultFilterRule = FileFilterRuleFactory.CreateCharacterInNameRule(charInput[0]);
                Console.WriteLine($"已选择字符匹配过滤规则: 包含字符 '{charInput[0]}'");
            }
            break;
        default:
            Console.WriteLine($"使用默认规则（所有文件）");
            break;
    }
}

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
    
    // 询问是否设置文件扩展名过滤
    Console.WriteLine("是否要设置文件扩展名过滤？(y/n，默认为n): ");
    string? extFilterInput = Console.ReadLine();
    if (!string.IsNullOrEmpty(extFilterInput) && extFilterInput.ToLower().StartsWith("y"))
    {
        Console.Write("请输入允许的扩展名，用逗号分隔 (例如: .cs,.txt,.pdf): ");
        string? extInput = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(extInput))
        {
            criteria.AllowedExtensions = extInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            Console.WriteLine($"已设置扩展名过滤: {string.Join(", ", criteria.AllowedExtensions)}");
        }
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
