using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FileRecord.Utils
{
    /// <summary>
    /// ??????
    /// </summary>
    public class FileFilterRule
    {
        /// <summary>
        /// ????
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// ???????????? .txt, .pdf, .docx?
        /// ????????????
        /// </summary>
        public string[] AllowedExtensions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// ???????????? .tmp, .temp?
        /// </summary>
        public string[] ExcludedExtensions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// ???????????????
        /// ????????????
        /// </summary>
        public string[] FileNamePatterns { get; set; } = Array.Empty<string>();

        /// <summary>
        /// ???????????????
        /// </summary>
        public string[] ExcludedFileNamePatterns { get; set; } = Array.Empty<string>();

        /// <summary>
        /// ??????????
        /// </summary>
        public long MinFileSize { get; set; } = 0;
        public long MaxFileSize { get; set; } = long.MaxValue;

        /// <summary>
        /// ??????????
        /// </summary>
        public bool EnableTempFileFiltering { get; set; } = true;

        /// <summary>
        /// ????????????
        /// </summary>
        /// <param name="filePath">????</param>
        /// <param name="fileSize">????</param>
        /// <returns>??????????true?????false</returns>
        public bool IsFileAllowed(string filePath, long fileSize = 0)
        {
            // ??????
            if (fileSize < MinFileSize || fileSize > MaxFileSize)
            {
                return false;
            }

            string fileName = Path.GetFileName(filePath);
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            // ?????????????????????
            if (EnableTempFileFiltering && FileUtils.IsTemporaryFile(filePath))
            {
                return false;
            }

            // ????????
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

            // ???????????????????????
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

            // ??????????
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
                        // ???????????????
                        continue;
                    }
                }
            }

            // ????????????????????????
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
                        // ???????????????
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
        /// ????????????
        /// </summary>
        /// <param name="extension">??????? .txt?</param>
        /// <returns>??????????true?????false</returns>
        public bool IsExtensionAllowed(string extension)
        {
            string ext = extension.ToLowerInvariant();

            // ????????
            foreach (var excludedExt in ExcludedExtensions)
            {
                if (ext == excludedExt.ToLowerInvariant())
                {
                    return false;
                }
            }

            // ????????
            if (AllowedExtensions.Length > 0)
            {
                foreach (var allowedExt in AllowedExtensions)
                {
                    if (ext == allowedExt.ToLowerInvariant())
                    {
                        return true;
                    }
                }
                return false; // ???????????????????
            }

            return true; // ????????????????
        }
    }
}