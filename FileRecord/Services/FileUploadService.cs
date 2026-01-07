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
        private readonly string _backupDirectory;
        
        public FileUploadService(DatabaseContext databaseContext, string backupDirectory = "bak")
        {
            _databaseContext = databaseContext;
            _backupDirectory = backupDirectory;
            
            // ????????
            if (!Directory.Exists(_backupDirectory))
            {
                Directory.CreateDirectory(_backupDirectory);
            }
        }
        
        public bool UploadFile(FileInfoModel fileInfo)
        {
            try
            {
                // ?????????
                if (!File.Exists(fileInfo.FilePath))
                {
                    Console.WriteLine($"??????: {fileInfo.FilePath}");
                    return false;
                }
                
                // ?????? - ??????????????
                string relativePath = GetRelativePath(fileInfo.FilePath, fileInfo.DirectoryPath);
                string targetDirectory = Path.Combine(_backupDirectory, fileInfo.DirectoryPath.Replace(Path.GetPathRoot(fileInfo.DirectoryPath) ?? "", "").TrimStart('\\', '/'));
                
                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }
                
                string targetPath = Path.Combine(targetDirectory, fileInfo.FileName);
                
                // ?????????
                File.Copy(fileInfo.FilePath, targetPath, true);
                
                // ???????????
                _databaseContext.MarkFileAsUploaded(fileInfo.Id, DateTime.Now);
                
                Console.WriteLine($"??????: {fileInfo.FileName} -> {targetPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"??????? {fileInfo.FileName}: {ex.Message}");
                return false;
            }
        }
        
        public void UploadAllUnuploadedFiles()
        {
            Console.WriteLine("????????????...");
            
            var unuploadedFiles = _databaseContext.GetUnuploadedFiles();
            int totalFiles = unuploadedFiles.Count;
            int successCount = 0;
            int failCount = 0;
            
            Console.WriteLine($"?? {totalFiles} ???????");
            
            foreach (var fileInfo in unuploadedFiles)
            {
                if (UploadFile(fileInfo))
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }
            
            Console.WriteLine($"????: {successCount} ??, {failCount} ??, ?? {totalFiles} ???");
        }
        
        public void UploadNewOrModifiedFile(string filePath)
        {
            try
            {
                // ?????????????
                bool fileExistsInDb = _databaseContext.FileExists(filePath);
                
                if (!fileExistsInDb)
                {
                    // ??????????????????
                    var fileInfo = new FileInfoModel(filePath);
                    _databaseContext.InsertFileInfo(fileInfo);
                }
                
                // ???????????
                // ???????????????FileInfoModel????????????
                var fileInfoForUpload = new FileInfoModel(filePath);
                
                // ????????????????ID
                var existingFileInfos = _databaseContext.GetUnuploadedFiles();
                var fileInfoInDb = existingFileInfos.Find(f => f.FilePath == filePath);
                
                if (fileInfoInDb != null)
                {
                    UploadFile(fileInfoInDb);
                }
                else
                {
                    // ??????????????????????????ID
                    var allFiles = GetFileInfoByPath(filePath);
                    if (allFiles != null && allFiles.Id > 0)
                    {
                        // ??????FileInfoModel????
                        allFiles.IsUploaded = false; // ???????????
                        _databaseContext.InsertFileInfo(allFiles); // ???????
                        UploadFile(allFiles);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"????????????? {filePath}: {ex.Message}");
            }
        }
        
        // ???????????
        private string GetRelativePath(string fullPath, string basePath)
        {
            if (string.IsNullOrEmpty(basePath))
                return Path.GetFileName(fullPath);
                
            Uri baseUri = new Uri(basePath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? basePath : basePath + Path.DirectorySeparatorChar);
            Uri fullUri = new Uri(fullPath);
            
            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }
        
        // ???????????????
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