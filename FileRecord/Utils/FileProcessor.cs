using System;
using System.IO;
using FileRecord.Models;

namespace FileRecord.Utils
{
    /// <summary>
    /// 文件处理器，提供统一的文件处理功能
    /// 用于简化文件信息创建、MD5计算、过滤检查等重复逻辑
    /// </summary>
    public static class FileProcessor
    {
        /// <summary>
        /// 创建文件信息模型，自动计算MD5并设置监控组ID
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="monitorGroupId">监控组ID</param>
        /// <param name="calculateMd5">是否计算MD5</param>
        /// <returns>文件信息模型</returns>
        public static FileInfoModel CreateFileInfoModel(string filePath, string monitorGroupId, bool calculateMd5 = true)
        {
            var fileInfo = new FileInfoModel(filePath)
            {
                MonitorGroupId = monitorGroupId,
                IsDeleted = false
            };

            if (calculateMd5)
            {
                CalculateAndSetMd5(fileInfo, filePath);
            }

            return fileInfo;
        }

        /// <summary>
        /// 计算并设置文件的MD5值
        /// </summary>
        /// <param name="fileInfo">文件信息模型</param>
        /// <param name="filePath">文件路径（可选，默认使用fileInfo.FilePath）</param>
        /// <param name="silent">是否静默处理错误（不输出控制台信息）</param>
        /// <returns>是否成功计算MD5</returns>
        public static bool CalculateAndSetMd5(FileInfoModel fileInfo, string? filePath = null, bool silent = false)
        {
            var path = filePath ?? fileInfo.FilePath;
            
            try
            {
                fileInfo.MD5Hash = FileUtils.CalculateMD5(path);
                return true;
            }
            catch (Exception ex)
            {
                if (!silent)
                {
                    Console.WriteLine($"计算MD5失败 {Path.GetFileName(path)}: {ex.Message}");
                }
                fileInfo.MD5Hash = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// 检查文件是否应被处理（通过过滤规则和临时文件检查）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="filterRule">过滤规则</param>
        /// <param name="fileSize">文件大小（可选）</param>
        /// <param name="silent">是否静默处理（不输出控制台信息）</param>
        /// <returns>如果文件应该被处理返回true</returns>
        public static bool ShouldProcessFile(string filePath, FileFilterRule? filterRule, long fileSize = 0, bool silent = false)
        {
            // 检查过滤规则
            if (filterRule != null && !filterRule.IsFileAllowed(filePath, fileSize))
            {
                if (!silent)
                {
                    Console.WriteLine($"跳过不符合过滤规则的文件: {Path.GetFileName(filePath)}");
                }
                return false;
            }

            // 检查是否为临时文件
            if (FileUtils.IsTemporaryFile(filePath))
            {
                if (!silent)
                {
                    Console.WriteLine($"跳过临时文件: {Path.GetFileName(filePath)}");
                }
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查文件是否应被处理（使用FileInfo）
        /// </summary>
        /// <param name="fileInfo">文件信息</param>
        /// <param name="filterRule">过滤规则</param>
        /// <param name="silent">是否静默处理</param>
        /// <returns>如果文件应该被处理返回true</returns>
        public static bool ShouldProcessFile(FileInfo fileInfo, FileFilterRule? filterRule, bool silent = false)
        {
            return ShouldProcessFile(fileInfo.FullName, filterRule, fileInfo.Length, silent);
        }

        /// <summary>
        /// 安全地获取文件信息，如果文件不存在或无法访问则返回null
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件信息或null</returns>
        public static FileInfo? SafeGetFileInfo(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                return new FileInfo(filePath);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 处理现有文件（用于初始化扫描），返回成功处理的文件数量
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <param name="monitorGroupId">监控组ID</param>
        /// <param name="filterRule">过滤规则</param>
        /// <param name="processAction">处理每个文件的动作</param>
        /// <returns>处理的文件数量</returns>
        public static int ProcessExistingFiles(
            string folderPath, 
            string monitorGroupId, 
            FileFilterRule? filterRule,
            Action<FileInfoModel> processAction)
        {
            if (!Directory.Exists(folderPath))
                return 0;

            var allFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            int processedCount = 0;

            foreach (var filePath in allFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);

                    if (!ShouldProcessFile(fileInfo, filterRule))
                        continue;

                    var fileInfoModel = CreateFileInfoModel(filePath, monitorGroupId);
                    processAction(fileInfoModel);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理现有文件 {filePath} 时出错: {ex.Message}");
                }
            }

            return processedCount;
        }
    }
}
