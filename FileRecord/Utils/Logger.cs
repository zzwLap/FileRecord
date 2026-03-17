using System;

namespace FileRecord.Utils
{
    /// <summary>
    /// 简单日志工具类，统一日志输出
    /// 后续可扩展为文件日志、结构化日志等
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// 输出信息日志
        /// </summary>
        public static void Info(string message)
        {
            Console.WriteLine($"[INFO] {message}");
        }

        /// <summary>
        /// 输出警告日志
        /// </summary>
        public static void Warn(string message)
        {
            Console.WriteLine($"[WARN] {message}");
        }

        /// <summary>
        /// 输出错误日志
        /// </summary>
        public static void Error(string message)
        {
            Console.WriteLine($"[ERROR] {message}");
        }

        /// <summary>
        /// 输出错误日志（带异常）
        /// </summary>
        public static void Error(string message, Exception ex)
        {
            Console.WriteLine($"[ERROR] {message}: {ex.Message}");
        }

        /// <summary>
        /// 输出调试日志
        /// </summary>
        public static void Debug(string message)
        {
#if DEBUG
            Console.WriteLine($"[DEBUG] {message}");
#endif
        }

        /// <summary>
        /// 输出纯文本（无级别前缀）
        /// </summary>
        public static void Write(string message)
        {
            Console.WriteLine(message);
        }

        /// <summary>
        /// 格式化输出
        /// </summary>
        public static void Write(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }
    }
}
