using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FileRecord.Services.Upload
{
    /// <summary>
    /// 上传配置管理器
    /// </summary>
    public class UploadConfigManager
    {
        private readonly string _configFilePath;
        private List<UploadTargetConfig> _configs;

        public UploadConfigManager(string configFilePath = "upload_configs.json")
        {
            _configFilePath = configFilePath;
            _configs = new List<UploadTargetConfig>();
            LoadConfigs();
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfigs()
        {
            if (File.Exists(_configFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_configFilePath);
                    var configs = JsonConvert.DeserializeObject<List<UploadTargetConfig>>(json);
                    _configs = configs ?? new List<UploadTargetConfig>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"加载配置失败: {ex.Message}");
                    _configs = new List<UploadTargetConfig>();
                }
            }
            else
            {
                // 创建默认配置文件
                SaveConfigs();
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private void SaveConfigs()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_configs, Formatting.Indented);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加配置项
        /// </summary>
        /// <param name="config">配置项</param>
        public void AddConfig(UploadTargetConfig config)
        {
            // 检查ID是否重复
            if (_configs.Exists(c => c.Id == config.Id))
            {
                throw new ArgumentException($"配置ID '{config.Id}' 已存在");
            }
            
            _configs.Add(config);
            SaveConfigs();
        }

        /// <summary>
        /// 更新配置项
        /// </summary>
        /// <param name="config">配置项</param>
        public void UpdateConfig(UploadTargetConfig config)
        {
            int index = _configs.FindIndex(c => c.Id == config.Id);
            if (index >= 0)
            {
                _configs[index] = config;
                SaveConfigs();
            }
            else
            {
                throw new ArgumentException($"配置ID '{config.Id}' 不存在");
            }
        }

        /// <summary>
        /// 移除配置项
        /// </summary>
        /// <param name="configId">配置ID</param>
        public void RemoveConfig(string configId)
        {
            _configs.RemoveAll(c => c.Id == configId);
            SaveConfigs();
        }

        /// <summary>
        /// 获取配置项
        /// </summary>
        /// <param name="configId">配置ID</param>
        /// <returns>如果找到配置项则返回，否则返回null</returns>
        public UploadTargetConfig? GetConfig(string configId)
        {
            return _configs.Find(c => c.Id == configId);
        }

        /// <summary>
        /// 获取所有配置项
        /// </summary>
        /// <returns>所有配置项的列表</returns>
        public List<UploadTargetConfig> GetAllConfigs()
        {
            return new List<UploadTargetConfig>(_configs);
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        /// <param name="config">配置项</param>
        /// <returns>如果配置有效返回true，否则返回false</returns>
        public bool ValidateConfig(UploadTargetConfig config)
        {
            if (string.IsNullOrEmpty(config.Id))
            {
                Console.WriteLine("配置ID不能为空");
                return false;
            }

            if (string.IsNullOrEmpty(config.Name))
            {
                Console.WriteLine("配置名称不能为空");
                return false;
            }

            // 根据不同的上传目标类型验证配置
            switch (config.TargetType)
            {
                case UploadTargetType.Local:
                    if (string.IsNullOrEmpty(config.TargetPath))
                    {
                        Console.WriteLine("本地上传必须指定目标路径");
                        return false;
                    }
                    break;
                case UploadTargetType.FTP:
                    if (string.IsNullOrEmpty(config.TargetPath) || 
                        string.IsNullOrEmpty(config.Credentials.Username) || 
                        string.IsNullOrEmpty(config.Credentials.Password))
                    {
                        Console.WriteLine("FTP上传必须指定目标路径、用户名和密码");
                        return false;
                    }
                    break;
                case UploadTargetType.S3:
                    if (string.IsNullOrEmpty(config.Credentials.AccessKeyId) || 
                        string.IsNullOrEmpty(config.Credentials.SecretAccessKey) ||
                        string.IsNullOrEmpty(config.Credentials.Endpoint) ||
                        string.IsNullOrEmpty(config.Credentials.ContainerName))
                    {
                        Console.WriteLine("S3上传必须指定访问密钥、秘密密钥、端点和容器名");
                        return false;
                    }
                    break;
                case UploadTargetType.AzureBlob:
                    if (string.IsNullOrEmpty(config.Credentials.AccessKeyId) || 
                        string.IsNullOrEmpty(config.Credentials.Endpoint) ||
                        string.IsNullOrEmpty(config.Credentials.ContainerName))
                    {
                        Console.WriteLine("Azure Blob上传必须指定访问密钥、端点和容器名");
                        return false;
                    }
                    break;
                case UploadTargetType.HTTP:
                    if (string.IsNullOrEmpty(config.TargetPath))
                    {
                        Console.WriteLine("HTTP上传必须指定目标URL");
                        return false;
                    }
                    break;
            }

            return true;
        }
    }
}