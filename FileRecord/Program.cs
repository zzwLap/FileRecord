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

// 获取要监听的文件夹路径
Console.Write("请输入要监听的文件夹路径: ");
string folderPath = Console.ReadLine() ?? "";

if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
{
    Console.WriteLine("无效的文件夹路径！");
    Console.WriteLine("按任意键退出...");
    Console.ReadKey();
    return;
}

// 创建数据库上下文
var databaseContext = new DatabaseContext();

// 创建上传服务
var uploadService = new FileUploadService(databaseContext);

// 创建文件夹监听服务
using var watcherService = new FolderWatcherService(folderPath, databaseContext, uploadService);

// 处理现有文件
watcherService.ProcessExistingFiles();

// 开始监听
watcherService.StartWatching();

// 立即上传所有未上传的文件
uploadService.UploadAllUnuploadedFiles();

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
        uploadService.UploadAllUnuploadedFiles();
        Console.WriteLine("程序正在运行，输入 'q' 并回车退出，输入 'view' 并回车查看数据库内容，输入 'upload' 并回车上传所有未上传文件...");
    }
}
