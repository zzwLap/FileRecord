using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileRecord.Services.Upload
{
    /// <summary>
    /// 上传目标工厂类
    /// </summary>
    public static class UploadTargetFactory
    {
        private static readonly Dictionary<UploadTargetType, Func<IUploadTarget>> _targetCreators;

        static UploadTargetFactory()
        {
            _targetCreators = new Dictionary<UploadTargetType, Func<IUploadTarget>>
            {
                { UploadTargetType.Local, () => new LocalUploadTarget() }
                // 可以在这里添加更多上传目标类型
            };
        }

        /// <summary>
        /// 创建上传目标实例
        /// </summary>
        /// <param name="targetType">上传目标类型</param>
        /// <returns>上传目标实例</returns>
        public static IUploadTarget CreateUploadTarget(UploadTargetType targetType)
        {
            if (_targetCreators.TryGetValue(targetType, out var creator))
            {
                return creator();
            }

            throw new ArgumentException($"不支持的上传目标类型: {targetType}");
        }

        /// <summary>
        /// 创建并初始化上传目标实例
        /// </summary>
        /// <param name="config">上传配置</param>
        /// <returns>初始化后的上传目标实例</returns>
        public static async Task<IUploadTarget> CreateAndInitializeUploadTargetAsync(UploadTargetConfig config)
        {
            var target = CreateUploadTarget(config.TargetType);
            await target.InitializeAsync(config);
            return target;
        }

        /// <summary>
        /// 注册新的上传目标类型和创建器
        /// </summary>
        /// <param name="targetType">上传目标类型</param>
        /// <param name="creator">创建器</param>
        public static void RegisterUploadTarget(UploadTargetType targetType, Func<IUploadTarget> creator)
        {
            _targetCreators[targetType] = creator;
        }
    }
}