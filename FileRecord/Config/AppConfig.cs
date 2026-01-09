using System;

namespace FileRecord.Config
{
    /// <summary>
    /// 应用程序配置类
    /// </summary>
    public static class AppConfig
    {
        /// <summary>
        /// 默认批量处理大小
        /// </summary>
        public const int DefaultBatchSize = 100;

        /// <summary>
        /// 文件系统监视器等待文件操作完成的时间（毫秒）
        /// </summary>
        public const int FileOperationWaitTimeMs = 100;

        /// <summary>
        /// 并行处理的最大并发度（默认为处理器核心数）
        /// </summary>
        public static int MaxParallelism => Environment.ProcessorCount;

        /// <summary>
        /// 数据库连接超时时间（秒）
        /// </summary>
        public const int DatabaseTimeoutSeconds = 30;

        /// <summary>
        /// 文件导入进度报告间隔（已处理文件数量）
        /// </summary>
        public const int ProgressReportInterval = 100;

        /// <summary>
        /// 临时文件过滤器启用状态
        /// </summary>
        public const bool EnableTempFileFiltering = true;
    }
}