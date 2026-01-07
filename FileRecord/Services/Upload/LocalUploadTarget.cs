using System;
using System.IO;
using System.Threading.Tasks;

namespace FileRecord.Services.Upload
{
    /// <summary>
    /// 本地上传目标实现
    /// </summary>
    public class LocalUploadTarget : IUploadTarget
    {
        private UploadTargetConfig? _config;

        public async Task InitializeAsync(UploadTargetConfig config)
        {
            _config = config;
            
            // 创建目标目录
            if (!Directory.Exists(_config.TargetPath))
            {
                Directory.CreateDirectory(_config.TargetPath);
            }
            
            await Task.CompletedTask;
        }

        public async Task<bool> UploadFileAsync(string sourceFilePath, string targetRelativePath)
        {
            if (_config == null)
            {
                throw new InvalidOperationException("Upload target not initialized");
            }

            try
            {
                // 构建目标路径
                string targetPath = Path.Combine(_config.TargetPath, targetRelativePath);
                
                // 确保目标目录存在
                string? targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // 复制文件到目标位置
                File.Copy(sourceFilePath, targetPath, true);
                
                Console.WriteLine($"文件上传成功: {sourceFilePath} -> {targetPath}");
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"文件上传失败: {ex.Message}");
                return await Task.FromResult(false);
            }
        }

        public async Task<bool> ValidateConfigAsync()
        {
            if (_config == null)
            {
                return await Task.FromResult(false);
            }

            try
            {
                // 测试目标目录的写权限
                string testPath = Path.Combine(_config.TargetPath, ".test_write_permission");
                File.WriteAllText(testPath, "test");
                File.Delete(testPath);
                return await Task.FromResult(true);
            }
            catch
            {
                return await Task.FromResult(false);
            }
        }

        public UploadTargetType GetTargetType()
        {
            return UploadTargetType.Local;
        }
    }
}