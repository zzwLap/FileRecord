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
            
            var selectSql = "SELECT Id, FileName, FilePath, FileSize, CreatedTime, ModifiedTime, Extension, DirectoryPath, IsUploaded, UploadTime FROM FileInfos ORDER BY CreatedTime DESC";
            
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
                    IsUploaded = reader.GetInt32(8) == 1,
                    UploadTime = reader.IsDBNull(9) ? (DateTime?)null : DateTime.Parse(reader.GetString(9))
                };
                
                files.Add(fileInfo);
            }
            
            return files;
        }
        
        public void PrintAllFiles()
        {
            var files = GetAllFiles();
            
            Console.WriteLine($"?????? {files.Count} ??????");
            Console.WriteLine(new string('-', 100));
            Console.WriteLine($"{"ID",-3} {"???",-20} {"??",-10} {"????",-20} {"????",-20} {"???",-8} {"???",-8} {"????",-20} {"??"}");
            Console.WriteLine(new string('-', 100));
            
            foreach (var file in files)
            {
                var uploadStatus = file.IsUploaded ? "?" : "?";
                var uploadTimeStr = file.UploadTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
                Console.WriteLine($"{file.Id,-3} {file.FileName,-20} {file.FileSize,-10} {file.CreatedTime:yyyy-MM-dd HH:mm:ss,-20} {file.ModifiedTime:yyyy-MM-dd HH:mm:ss,-20} {file.Extension,-8} {uploadStatus,-8} {uploadTimeStr,-20} {file.DirectoryPath}");
            }
        }
    }
}