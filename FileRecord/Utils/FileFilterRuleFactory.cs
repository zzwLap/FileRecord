using System;

namespace FileRecord.Utils
{
    /// <summary>
    /// ??????????????????????
    /// </summary>
    public static class FileFilterRuleFactory
    {
        /// <summary>
        /// ???????????? .doc, .docx, .pdf, .txt, .xls, .xlsx ??
        /// </summary>
        public static FileFilterRule CreateDocumentRule(string name = "????")
        {
            return new FileFilterRule
            {
                Name = name,
                AllowedExtensions = new[] { ".doc", ".docx", ".pdf", ".txt", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp" },
                ExcludedExtensions = new[] { ".tmp", ".temp", ".log" },
                EnableTempFileFiltering = true
            };
        }

        /// <summary>
        /// ???????????? .jpg, .png, .gif, .bmp ??
        /// </summary>
        public static FileFilterRule CreateImageRule(string name = "????")
        {
            return new FileFilterRule
            {
                Name = name,
                AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".svg", ".webp" },
                ExcludedExtensions = new[] { ".tmp", ".temp" },
                EnableTempFileFiltering = true
            };
        }

        /// <summary>
        /// ???????????? .mp4, .avi, .mkv, .mov ??
        /// </summary>
        public static FileFilterRule CreateVideoRule(string name = "????")
        {
            return new FileFilterRule
            {
                Name = name,
                AllowedExtensions = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" },
                ExcludedExtensions = new[] { ".tmp", ".temp" },
                EnableTempFileFiltering = true
            };
        }

        /// <summary>
        /// ???????????? .mp3, .wav, .flac, .aac ??
        /// </summary>
        public static FileFilterRule CreateAudioRule(string name = "????")
        {
            return new FileFilterRule
            {
                Name = name,
                AllowedExtensions = new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma" },
                ExcludedExtensions = new[] { ".tmp", ".temp" },
                EnableTempFileFiltering = true
            };
        }

        /// <summary>
        /// ???????????? .cs, .js, .ts, .py, .java ??
        /// </summary>
        public static FileFilterRule CreateCodeRule(string name = "????")
        {
            return new FileFilterRule
            {
                Name = name,
                AllowedExtensions = new[] { ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".c", ".h", ".html", ".css", ".json", ".xml", ".sql", ".php", ".go", ".rs", ".rb", ".swift", ".kt" },
                ExcludedExtensions = new[] { ".tmp", ".temp", ".log", ".dll", ".exe", ".bin" },
                EnableTempFileFiltering = true
            };
        }

        /// <summary>
        /// ????????????
        /// </summary>
        /// <param name="allowedExtensions">?????????? new[] {".txt", ".pdf"}?</param>
        /// <param name="name">????</param>
        /// <param name="excludedExtensions">????????</param>
        public static FileFilterRule CreateCustomRule(
            string[] allowedExtensions, 
            string name = "?????", 
            string[]? excludedExtensions = null)
        {
            return new FileFilterRule
            {
                Name = name,
                AllowedExtensions = allowedExtensions,
                ExcludedExtensions = excludedExtensions ?? Array.Empty<string>(),
                EnableTempFileFiltering = true
            };
        }

        /// <summary>
        /// ???????????
        /// </summary>
        /// <param name="fileNamePatterns">???????????????</param>
        /// <param name="excludedPatterns">???????????????</param>
        /// <param name="name">????</param>
        public static FileFilterRule CreatePatternRule(
            string[] fileNamePatterns,
            string[]? excludedPatterns = null,
            string name = "????")
        {
            return new FileFilterRule
            {
                Name = name,
                FileNamePatterns = fileNamePatterns,
                ExcludedFileNamePatterns = excludedPatterns ?? Array.Empty<string>(),
                EnableTempFileFiltering = true
            };
        }

        /// <summary>
        /// ??????????
        /// </summary>
        /// <param name="minSize">??????????</param>
        /// <param name="maxSize">??????????</param>
        /// <param name="name">????</param>
        /// <param name="allowedExtensions">??????????</param>
        public static FileFilterRule CreateSizeRule(
            long minSize,
            long maxSize,
            string name = "??????",
            string[]? allowedExtensions = null)
        {
            return new FileFilterRule
            {
                Name = name,
                MinFileSize = minSize,
                MaxFileSize = maxSize,
                AllowedExtensions = allowedExtensions ?? Array.Empty<string>(),
                EnableTempFileFiltering = true
            };
        }
    }
}