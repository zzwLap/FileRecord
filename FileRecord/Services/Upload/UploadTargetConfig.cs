using System;

namespace FileRecord.Services.Upload
{
    /// <summary>
    /// 上传目标配置类
    /// </summary>
    public class UploadTargetConfig
    {
        /// <summary>
        /// 配置ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 配置名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 上传目标类型
        /// </summary>
        public UploadTargetType TargetType { get; set; }

        /// <summary>
        /// 上传目标URL
        /// </summary>
        public string TargetPath { get; set; } = string.Empty;

        /// <summary>
        /// 上传凭证
        /// </summary>
        public UploadCredentials Credentials { get; set; } = new UploadCredentials();

        /// <summary>
        /// 路径映射规则
        /// </summary>
        public string PathMappingRule { get; set; } = string.Empty;

        /// <summary>
        /// 上传超时时间
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300; // 默认5分钟

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// 是否启用压缩
        /// </summary>
        public bool EnableCompression { get; set; } = false;
    }

    /// <summary>
    /// 上传目标类型枚举
    /// </summary>
    public enum UploadTargetType
    {
        Local,      // 本地
        FTP,        // FTP服务器
        S3,         // AWS S3
        AzureBlob,  // Azure Blob Storage
        HTTP,       // HTTP API
        Custom      // 自定义
    }

    /// <summary>
    /// 上传凭证类
    /// </summary>
    public class UploadCredentials
    {
        /// <summary>
        /// 访问密钥ID
        /// </summary>
        public string AccessKeyId { get; set; } = string.Empty;

        /// <summary>
        /// 秘密访问密钥
        /// </summary>
        public string SecretAccessKey { get; set; } = string.Empty;

        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 令牌
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// 端点/地址
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// 容器名/桶名
        /// </summary>
        public string ContainerName { get; set; } = string.Empty;
    }
}