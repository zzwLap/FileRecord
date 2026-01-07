using System;
using System.Text.RegularExpressions;

namespace FileRecord.Utils
{
    /// <summary>
    /// 文件过滤规则工厂类，用于创建各种预定义的过滤规则
    /// </summary>
    public static class FileFilterRuleFactory
    {
        /// <summary>
        /// 创建文档文件过滤规则，允许常见的文档格式如 .doc, .docx, .pdf, .txt, .xls, .xlsx 等
        /// </summary>
        public static FileFilterRule CreateDocumentRule(string name = "文档文件")
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
        /// 创建图片文件过滤规则，允许常见的图片格式如 .jpg, .png, .gif, .bmp 等
        /// </summary>
        public static FileFilterRule CreateImageRule(string name = "图片文件")
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
        /// 创建视频文件过滤规则，允许常见的视频格式如 .mp4, .avi, .mkv, .mov 等
        /// </summary>
        public static FileFilterRule CreateVideoRule(string name = "视频文件")
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
        /// 创建音频文件过滤规则，允许常见的音频格式如 .mp3, .wav, .flac, .aac 等
        /// </summary>
        public static FileFilterRule CreateAudioRule(string name = "音频文件")
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
        /// 创建代码文件过滤规则，允许常见的代码格式如 .cs, .js, .ts, .py, .java 等
        /// </summary>
        public static FileFilterRule CreateCodeRule(string name = "代码文件")
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
        /// 创建自定义扩展名过滤规则
        /// </summary>
        /// <param name="allowedExtensions">允许的扩展名数组，如 new[] {".txt", ".pdf"}?</param>
        /// <param name="name">规则名称</param>
        /// <param name="excludedExtensions">排除的扩展名数组</param>
        public static FileFilterRule CreateCustomRule(
            string[] allowedExtensions, 
            string name = "自定义规则", 
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
        /// 创建文件名模式过滤规则
        /// </summary>
        /// <param name="fileNamePatterns">允许的文件名正则表达式模式数组</param>
        /// <param name="excludedPatterns">排除的文件名正则表达式模式数组</param>
        /// <param name="name">规则名称</param>
        public static FileFilterRule CreatePatternRule(
            string[] fileNamePatterns,
            string[]? excludedPatterns = null,
            string name = "模式规则")
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
        /// 创建文件大小过滤规则
        /// </summary>
        /// <param name="minSize">最小文件大小（字节）</param>
        /// <param name="maxSize">最大文件大小（字节）</param>
        /// <param name="name">规则名称</param>
        /// <param name="allowedExtensions">允许的扩展名数组</param>
        public static FileFilterRule CreateSizeRule(
            long minSize,
            long maxSize,
            string name = "大小规则",
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

        /// <summary>
        /// 创建包含特定字符的文件名过滤规则，例如 *a.* 将匹配文件名中包含字母 a 的文件
        /// </summary>
        /// <param name="character">要匹配的字符</param>
        /// <param name="name">规则名称</param>
        public static FileFilterRule CreateCharacterInNameRule(char character, string name = "字符匹配规则")
        {
            // 正则表达式: .*a.*\..* 将匹配文件名中包含指定字符的文件
            string pattern = $@".*{Regex.Escape(character.ToString())}.*\..*";
            return new FileFilterRule
            {
                Name = name,
                FileNamePatterns = new[] { pattern },
                EnableTempFileFiltering = true
            };
        }

        /// <summary>
        /// 创建通配符过滤规则，例如 *a.* 将匹配文件名中包含字母 a 的文件
        /// </summary>
        /// <param name="wildcardPattern">通配符模式，如 *a.* </param>
        /// <param name="name">规则名称</param>
        public static FileFilterRule CreateWildcardRule(string wildcardPattern, string name = "通配符规则")
        {
            // 将通配符模式转换为正则表达式
            string regexPattern = ConvertWildcardToRegex(wildcardPattern);
            return new FileFilterRule
            {
                Name = name,
                FileNamePatterns = new[] { regexPattern },
                EnableTempFileFiltering = true
            };
        }

        /// <summary>
        /// 将通配符模式转换为正则表达式
        /// </summary>
        /// <param name="wildcard">通配符模式，如 *a.*, *.txt 等</param>
        /// <returns>对应的正则表达式</returns>
        private static string ConvertWildcardToRegex(string wildcard)
        {
            // 将通配符转换为正则表达式模式
            string regex = wildcard
                .Replace(".", "\\.")  // 转义点号
                .Replace("?", ".")   // ? 匹配任意单个字符
                .Replace("*", ".*"); // * 匹配任意多个字符
            
            return "^" + regex + "$"; // 完整匹配
        }
    }
}