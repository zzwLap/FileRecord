using System;
using System.IO;
using System.Collections.Generic;
using FileRecord.Data;
using FileRecord.Models;

namespace FileRecord.Services.Upload
{
    public class FileUploadService
    {
        private readonly DatabaseContext _databaseContext;
        private readonly UploadTaskManager _taskManager;
        
        public FileUploadService(DatabaseContext databaseContext, UploadTaskManager taskManager, string backupDirectory = "bak")
        {
            _databaseContext = databaseContext;
            _taskManager = taskManager;
        }
        
        public void EnqueueFileForUpload(int fileId, string filePath)
        {
            _taskManager.EnqueueUpload(fileId, filePath);
        }
        
        public void EnqueueNewOrModifiedFile(string filePath)
        {
            try
            {
                // 检查文件是否已经在数据库中
                bool fileExistsInDb = _databaseContext.FileExists(filePath);
                
                if (!fileExistsInDb)
                {
                    // 如果文件不在数据库中，先添加到数据库
                    var fileInfo = new FileInfoModel(filePath);
                    _databaseContext.InsertFileInfo(fileInfo);
                }
                
                // 从数据库获取完整的文件信息，包括ID
                var fileInfoFromDb = GetFileInfoByPath(filePath);
                
                if (fileInfoFromDb != null)
                {
                    // 如果文件已上传但发生了变化，标记为未上传状态，以便可以重新上传
                    if (fileInfoFromDb.IsUploaded)
                    {
                        _databaseContext.MarkFileAsUnuploaded(fileInfoFromDb.Id);
                        Console.WriteLine($"文件已修改，需要重新上传: {filePath}");
                    }
                    
                    // 将文件加入上传队列
                    _taskManager.EnqueueUpload(fileInfoFromDb.Id, fileInfoFromDb.FilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理新文件或修改文件时出错 {filePath}: {ex.Message}");
            }
        }
        
        public void EnqueueAllUnuploadedFiles()
        {
            _taskManager.EnqueueUnuploadedFiles();
        }
        
        // 从数据库获取特定路径的文件信息
        private FileInfoModel? GetFileInfoByPath(string filePath)
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_databaseContext.GetConnectionString());
            connection.Open();
            
            var selectSql = "SELECT Id, FileName, FilePath, FileSize, CreatedTime, ModifiedTime, Extension, DirectoryPath, IsUploaded, UploadTime FROM FileInfos WHERE FilePath = @FilePath";
            
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
                    IsUploaded = reader.GetInt32(8) == 1,
                    UploadTime = reader.IsDBNull(9) ? (DateTime?)null : DateTime.Parse(reader.GetString(9))
                };
            }
            
            return null;
        }
    }
}