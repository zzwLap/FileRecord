using System;
using System.IO;

namespace FileRecord.Models
{
    public class FileInfoModel
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime ModifiedTime { get; set; }
        public string Extension { get; set; } = string.Empty;
        public string DirectoryPath { get; set; } = string.Empty;
        public bool IsUploaded { get; set; } = false;
        public DateTime? UploadTime { get; set; } = null;
        public string MD5Hash { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        
        public FileInfoModel() { }
        
        public FileInfoModel(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            FileName = fileInfo.Name;
            FilePath = fileInfo.FullName;
            FileSize = fileInfo.Length;
            CreatedTime = fileInfo.CreationTime;
            ModifiedTime = fileInfo.LastWriteTime;
            Extension = fileInfo.Extension;
            DirectoryPath = fileInfo.DirectoryName ?? string.Empty;
        }
    }
}