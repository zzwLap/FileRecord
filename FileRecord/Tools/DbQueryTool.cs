using System;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using FileRecord.Models;

namespace FileRecord.Tools
{
    public class DbQueryTool
    {
        private readonly string _connectionString;
        
        public DbQueryTool(string dbPath = "fileinfo.db")
        {
            _connectionString = $"Data Source={dbPath}";
        }
        
        public List<FileInfoModel> GetAllFiles()
        {
            var files = new List<FileInfoModel>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var selectSql = "SELECT Id, FileName, FilePath, FileSize, CreatedTime, ModifiedTime, Extension, DirectoryPath, MonitorGroupId, IsUploaded, UploadTime, MD5Hash, IsDeleted FROM FileInfos ORDER BY CreatedTime DESC";
            
            using var command = new SqliteCommand(selectSql, connection);
            using var reader = command.ExecuteReader();
            
            while (reader.Read())
            {
                var fileInfo = new FileInfoModel
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
                
                files.Add(fileInfo);
            }
            
            return files;
        }
        
        public void PrintAllFiles()
        {
            var files = GetAllFiles();
            
            Console.WriteLine($"数据库中共有 {files.Count} 个文件记录：");
            Console.WriteLine(new string('-', 130));
            Console.WriteLine($"{"ID",-3} {"文件名",-20} {"大小",-10} {"创建时间",-20} {"修改时间",-20} {"扩展名",-8} {"已上传",-8} {"上传时间",-20} {"删除",-6} {"MD5",-10} {"监控组",-12} {"路径"}");
            Console.WriteLine(new string('-', 130));
            
            foreach (var file in files)
            {
                var uploadStatus = file.IsUploaded ? "是" : "否";
                var uploadTimeStr = file.UploadTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
                var deleteStatus = file.IsDeleted ? "是" : "否";
                var md5Short = file.MD5Hash.Length > 10 ? file.MD5Hash.Substring(0, 10) + "..." : file.MD5Hash;
                var monitorGroup = string.IsNullOrEmpty(file.MonitorGroupId) ? "default" : file.MonitorGroupId;
                Console.WriteLine($"{file.Id,-3} {file.FileName,-20} {file.FileSize,-10} {file.CreatedTime:yyyy-MM-dd HH:mm:ss,-20} {file.ModifiedTime:yyyy-MM-dd HH:mm:ss,-20} {file.Extension,-8} {uploadStatus,-8} {uploadTimeStr,-20} {deleteStatus,-6} {md5Short,-10} {monitorGroup,-12} {file.DirectoryPath}");
            }
        }
    }
}