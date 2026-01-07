using System;
using FileRecord.Data;
using FileRecord.Services;
using FileRecord.Services.Upload;
using FileRecord.Tools;

// 检查命令行参数
if (args.Length > 0 && args[0] == "--view")
{
    // 查看数据库内容
    var dbTool = new DbQueryTool();
    dbTool.PrintAllFiles();
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

// 解析路径列表
string[] folderPaths = folderPathsInput.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// 添加每个路径到监控列表
foreach (var path in folderPaths)
{
    if (Directory.Exists(path))
    {
        string monitorGroupId = $"Group_{Path.GetFileName(path)}"; // 使用目录名作为监控组ID
        multiWatcherService.AddFolderToWatch(path, monitorGroupId);
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
