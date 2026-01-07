using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FileRecord.Utils
{
    /// <summary>
    /// 文件过滤规则类
    /// </summary>
    public class FileFilterRule
    {
        /// <summary>
        /// 规则名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 允许的文件扩展名，如 .txt, .pdf, .docx等
        /// 如果设置了此属性，则只允许这些扩展名
        /// </summary>
        public string[] AllowedExtensions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 排除的文件扩展名，如 .tmp, .temp等
        /// </summary>
        public string[] ExcludedExtensions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 允许的文件名模式，使用正则表达式
        /// 如果设置了此属性，则只允许匹配这些模式的文件名
        /// </summary>
        public string[] FileNamePatterns { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 排除的文件名模式，使用正则表达式
        /// </summary>
        public string[] ExcludedFileNamePatterns { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 允许的文件大小范围
        /// 默认为0到long.MaxValue
        /// </summary>
        public long MinFileSize { get; set; } = 0;
        public long MaxFileSize { get; set; } = long.MaxValue;

        /// <summary>
        /// 是否启用临时文件过滤
        /// </summary>
        public bool EnableTempFileFiltering { get; set; } = true;

        /// <summary>
        /// 检查文件是否符合过滤规则
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="fileSize">文件大小</param>
        /// <returns>如果文件符合规则返回true，否则返回false</returns>
        public bool IsFileAllowed(string filePath, long fileSize = 0)
        {
            // 检查文件大小
            if (fileSize < MinFileSize || fileSize > MaxFileSize)
            {
                return false;
            }

            string fileName = Path.GetFileName(filePath);
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            // 检查是否为临时文件，如果是则过滤掉
            if (EnableTempFileFiltering && FileUtils.IsTemporaryFile(filePath))
            {
                return false;
            }

            // 检查排除的扩展名
            if (ExcludedExtensions.Length > 0)
            {
                foreach (var excludedExt in ExcludedExtensions)
                {
                    if (extension == excludedExt.ToLowerInvariant())
                    {
                        return false;
                    }
                }
            }

            // 检查允许的扩展名，如果设置了此属性，则只允许这些扩展名
            if (AllowedExtensions.Length > 0)
            {
                bool isAllowed = false;
                foreach (var allowedExt in AllowedExtensions)
                {
                    if (extension == allowedExt.ToLowerInvariant())
                    {
                        isAllowed = true;
                        break;
                    }
                }
                if (!isAllowed)
                {
                    return false;
                }
            }

            // 检查排除的文件名模式
            if (ExcludedFileNamePatterns.Length > 0)
            {
                foreach (var pattern in ExcludedFileNamePatterns)
                {
                    try
                    {
                        if (Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase))
                        {
                            return false;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // 如果正则表达式无效，则跳过该模式
                        continue;
                    }
                }
            }

            // 检查允许的文件名模式，如果设置了此属性，则只允许匹配这些模式的文件名
            if (FileNamePatterns.Length > 0)
            {
                bool isMatched = false;
                foreach (var pattern in FileNamePatterns)
                {
                    try
                    {
                        if (Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase))
                        {
                            isMatched = true;
                            break;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // 如果正则表达式无效，则跳过该模式
                        continue;
                    }
                }
                if (!isMatched)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 检查文件扩展名是否被允许
        /// </summary>
        /// <param name="extension">文件扩展名，如 .txt</param>
        /// <returns>如果扩展名被允许返回true，否则返回false</returns>
        public bool IsExtensionAllowed(string extension)
        {
            string ext = extension.ToLowerInvariant();

            // 检查排除的扩展名
            foreach (var excludedExt in ExcludedExtensions)
            {
                if (ext == excludedExt.ToLowerInvariant())
                {
                    return false;
                }
            }

            // 检查允许的扩展名
            if (AllowedExtensions.Length > 0)
            {
                foreach (var allowedExt in AllowedExtensions)
                {
                    if (ext == allowedExt.ToLowerInvariant())
                    {
                        return true;
                    }
                }
                return false; // 如果设置了允许的扩展名但当前扩展名不在其中，则返回false
            }

            return true; // 如果没有设置允许的扩展名，则默认所有扩展名都被允许
        }
    }
}