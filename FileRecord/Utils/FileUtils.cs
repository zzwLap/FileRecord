using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FileRecord.Utils
{
    public static class FileUtils
    {
        /// <summary>
        /// 计算文件的MD5哈希值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>MD5哈希值的十六进制字符串</returns>
        public static string CalculateMD5(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件不存在: {filePath}");
            }

            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hashBytes = md5.ComputeHash(stream);
            
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// 检查文件是否为临时文件，应被过滤
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>如果是临时文件返回true，否则返回false</returns>
        public static bool IsTemporaryFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            string fileName = Path.GetFileName(filePath);
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            // 检查文件扩展名
            if (extension == ".tmp" || extension == ".temp" || extension == ".tmp~" || extension == "~tmp")
                return true;

            // 检查文件名前缀
            if (fileName.StartsWith("~$") || // Office临时文件
                fileName.StartsWith(".") ||  // 隐藏文件（在Unix系统中）
                fileName.EndsWith("~"))      // Emacs和其他编辑器的备份文件
                return true;

            // 检查特定的临时文件模式
            string[] tempPatterns = { 
                ".bak", ".backup", ".old", ".orig", ".swp", ".swo", // 备份文件
                ".cache", ".tmpcache", // 缓存文件
                ".lock", ".lck" // 锁文件
            };

            foreach (string pattern in tempPatterns)
            {
                if (extension == pattern)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 检查两个文件的MD5是否相同
        /// </summary>
        /// <param name="filePath1">第一个文件路径</param>
        /// <param name="filePath2">第二个文件路径</param>
        /// <returns>如果MD5相同返回true，否则返回false</returns>
        public static bool AreFilesIdentical(string filePath1, string filePath2)
        {
            if (!File.Exists(filePath1) || !File.Exists(filePath2))
                return false;

            try
            {
                string md5_1 = CalculateMD5(filePath1);
                string md5_2 = CalculateMD5(filePath2);
                return md5_1.Equals(md5_2, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // 如果计算MD5失败，则比较文件大小和修改时间
                var info1 = new FileInfo(filePath1);
                var info2 = new FileInfo(filePath2);
                return info1.Length == info2.Length && 
                       Math.Abs((info1.LastWriteTime - info2.LastWriteTime).TotalSeconds) < 1;
            }
        }
    }
}