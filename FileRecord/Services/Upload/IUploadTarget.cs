using System.Threading.Tasks;

namespace FileRecord.Services.Upload
{
    /// <summary>
    /// 上传目标接口
    /// </summary>
    public interface IUploadTarget
    {
        /// <summary>
        /// 初始化上传目标
        /// </summary>
        /// <param name="config">上传配置</param>
        Task InitializeAsync(UploadTargetConfig config);

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="sourceFilePath">源文件路径</param>
        /// <param name="targetRelativePath">目标相对路径</param>
        /// <returns>如果上传成功返回true，否则返回false</returns>
        Task<bool> UploadFileAsync(string sourceFilePath, string targetRelativePath);

        /// <summary>
        /// 验证配置有效性
        /// </summary>
        /// <returns>如果配置有效返回true，否则返回false</returns>
        Task<bool> ValidateConfigAsync();

        /// <summary>
        /// 获取目标类型
        /// </summary>
        UploadTargetType GetTargetType();
    }
}