using System;
using Microsoft.Data.Sqlite;
using FileRecord.Models;

namespace FileRecord.Data
{
    public class DatabaseContext
    {
        private readonly string _connectionString;
        
        public DatabaseContext(string dbPath = "fileinfo.db")
        {
            _connectionString = $"Data Source={dbPath}";
            InitializeDatabase();
        }
        
        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS FileInfos (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FileName TEXT NOT NULL,
                    FilePath TEXT NOT NULL UNIQUE,
                    FileSize INTEGER NOT NULL,
                    CreatedTime TEXT NOT NULL,
                    ModifiedTime TEXT NOT NULL,
                    Extension TEXT,
                    DirectoryPath TEXT,
                    IsUploaded INTEGER NOT NULL DEFAULT 0,
                    UploadTime TEXT
                )";
            
            using var command = new SqliteCommand(createTableSql, connection);
            command.ExecuteNonQuery();
            
            // ???????????
            var alterTableSql = @"
                PRAGMA table_info(FileInfos)";
            
            using var checkCommand = new SqliteCommand(alterTableSql, connection);
            using var reader = checkCommand.ExecuteReader();
            
            bool hasIsUploaded = false;
            bool hasUploadTime = false;
            
            while (reader.Read())
            {
                var columnName = reader.GetString(1);
                if (columnName == "IsUploaded") hasIsUploaded = true;
                if (columnName == "UploadTime") hasUploadTime = true;
            }
            reader.Close();
            
            // ??IsUploaded????????
            if (!hasIsUploaded)
            {
                var addIsUploadedColumnSql = "ALTER TABLE FileInfos ADD COLUMN IsUploaded INTEGER NOT NULL DEFAULT 0";
                using var addIsUploadedCommand = new SqliteCommand(addIsUploadedColumnSql, connection);
                addIsUploadedCommand.ExecuteNonQuery();
            }
            
            // ??UploadTime????????
            if (!hasUploadTime)
            {
                var addUploadTimeColumnSql = "ALTER TABLE FileInfos ADD COLUMN UploadTime TEXT";
                using var addUploadTimeCommand = new SqliteCommand(addUploadTimeColumnSql, connection);
                addUploadTimeCommand.ExecuteNonQuery();
            }
        }
        
        public void InsertFileInfo(FileInfoModel fileInfo)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var insertSql = @"
                INSERT OR REPLACE INTO FileInfos 
                (FileName, FilePath, FileSize, CreatedTime, ModifiedTime, Extension, DirectoryPath, IsUploaded, UploadTime) 
                VALUES (@FileName, @FilePath, @FileSize, @CreatedTime, @ModifiedTime, @Extension, @DirectoryPath, @IsUploaded, @UploadTime)";
            
            using var command = new SqliteCommand(insertSql, connection);
            command.Parameters.AddWithValue("@FileName", fileInfo.FileName);
            command.Parameters.AddWithValue("@FilePath", fileInfo.FilePath);
            command.Parameters.AddWithValue("@FileSize", fileInfo.FileSize);
            command.Parameters.AddWithValue("@CreatedTime", fileInfo.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@ModifiedTime", fileInfo.ModifiedTime.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@Extension", fileInfo.Extension);
            command.Parameters.AddWithValue("@DirectoryPath", fileInfo.DirectoryPath);
            command.Parameters.AddWithValue("@IsUploaded", fileInfo.IsUploaded ? 1 : 0);
            command.Parameters.AddWithValue("@UploadTime", fileInfo.UploadTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
            
            command.ExecuteNonQuery();
        }
        
        public void DeleteFileInfo(string filePath)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var deleteSql = "DELETE FROM FileInfos WHERE FilePath = @FilePath";
            
            using var command = new SqliteCommand(deleteSql, connection);
            command.Parameters.AddWithValue("@FilePath", filePath);
            
            command.ExecuteNonQuery();
        }
        
        public bool FileExists(string filePath)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var selectSql = "SELECT COUNT(*) FROM FileInfos WHERE FilePath = @FilePath";
            
            using var command = new SqliteCommand(selectSql, connection);
            command.Parameters.AddWithValue("@FilePath", filePath);
            
            var result = command.ExecuteScalar();
            return Convert.ToInt32(result) > 0;
        }
        
        public List<FileInfoModel> GetUnuploadedFiles()
        {
            var files = new List<FileInfoModel>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var selectSql = "SELECT Id, FileName, FilePath, FileSize, CreatedTime, ModifiedTime, Extension, DirectoryPath, IsUploaded, UploadTime FROM FileInfos WHERE IsUploaded = 0 ORDER BY CreatedTime DESC";
            
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
        
        public void MarkFileAsUploaded(int fileId, DateTime uploadTime)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var updateSql = "UPDATE FileInfos SET IsUploaded = 1, UploadTime = @UploadTime WHERE Id = @Id";
            
            using var command = new SqliteCommand(updateSql, connection);
            command.Parameters.AddWithValue("@Id", fileId);
            command.Parameters.AddWithValue("@UploadTime", uploadTime.ToString("yyyy-MM-dd HH:mm:ss"));
            
            command.ExecuteNonQuery();
        }
        
        public void MarkFileAsUnuploaded(int fileId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var updateSql = "UPDATE FileInfos SET IsUploaded = 0, UploadTime = NULL WHERE Id = @Id";
            
            using var command = new SqliteCommand(updateSql, connection);
            command.Parameters.AddWithValue("@Id", fileId);
            
            command.ExecuteNonQuery();
        }
        
        public string GetConnectionString()
        {
            return _connectionString;
        }
    }
}