using System;

namespace FileRecord.Utils
{
    /// <summary>
    /// 格式化工具类，提供通用的格式化功能
    /// </summary>
    public static class FormatUtils
    {
        private static readonly string[] FileSizeUnits = { "B", "KB", "MB", "GB", "TB", "PB" };

        /// <summary>
        /// 格式化文件大小为人类可读的字符串
        /// </summary>
        /// <param name="bytes">字节数</param>
        /// <param name="decimalPlaces">小数位数</param>
        /// <returns>格式化的大小字符串（如 "1.5 MB"）</returns>
        public static string FormatFileSize(long bytes, int decimalPlaces = 2)
        {
            if (bytes < 0)
                return "0 B";

            if (bytes == 0)
                return "0 B";

            double size = bytes;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < FileSizeUnits.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            string format = decimalPlaces > 0 
                ? $"{{0:F{decimalPlaces}}} {{1}}" 
                : "{0:F0} {1}";

            return string.Format(format, size, FileSizeUnits[unitIndex]);
        }

        /// <summary>
        /// 格式化日期时间为标准字符串
        /// </summary>
        /// <param name="dateTime">日期时间</param>
        /// <param name="format">格式字符串（默认为 yyyy-MM-dd HH:mm:ss）</param>
        /// <returns>格式化后的字符串</returns>
        public static string FormatDateTime(DateTime dateTime, string format = "yyyy-MM-dd HH:mm:ss")
        {
            return dateTime.ToString(format);
        }

        /// <summary>
        /// 格式化日期为短格式字符串
        /// </summary>
        /// <param name="dateTime">日期时间</param>
        /// <returns>格式化后的字符串（yyyy-MM-dd）</returns>
        public static string FormatDate(DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// 截断字符串，如果超过指定长度则添加省略号
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <param name="maxLength">最大长度</param>
        /// <param name="suffix">后缀（默认为...）</param>
        /// <returns>截断后的字符串</returns>
        public static string Truncate(string input, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input ?? string.Empty;

            int truncateLength = maxLength - suffix.Length;
            if (truncateLength <= 0)
                return suffix;

            return input.Substring(0, truncateLength) + suffix;
        }

        /// <summary>
        /// 格式化时间范围为可读字符串
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>格式化后的字符串</returns>
        public static string FormatTimeRange(DateTime? startTime, DateTime? endTime)
        {
            string start = startTime.HasValue ? FormatDateTime(startTime.Value) : "无限制";
            string end = endTime.HasValue ? FormatDateTime(endTime.Value) : "无限制";
            return $"{start} - {end}";
        }

        /// <summary>
        /// 格式化文件数量为可读字符串
        /// </summary>
        /// <param name="count">文件数量</param>
        /// <returns>格式化后的字符串</returns>
        public static string FormatFileCount(int count)
        {
            return count == 0 ? "无文件" : $"{count} 个文件";
        }
    }
}
