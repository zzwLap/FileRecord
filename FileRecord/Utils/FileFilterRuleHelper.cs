using System;
using System.Threading.Tasks;

namespace FileRecord.Utils
{
    /// <summary>
    /// FileFilterRule辅助类，提供统一的过滤规则创建和配置功能
    /// </summary>
    public static class FileFilterRuleHelper
    {
        /// <summary>
        /// 交互式创建FileFilterRule
        /// </summary>
        /// <param name="promptMessage">提示消息</param>
        /// <returns>创建的FileFilterRule，如果用户选择取消或使用默认规则则返回null</returns>
        public static FileFilterRule? CreateInteractiveFilterRule(string promptMessage = "请选择过滤规则类型:")
        {
            Console.WriteLine(promptMessage);
            ShowFilterRuleOptions();
            
            string? choice = Console.ReadLine();
            return CreateFilterRuleByChoice(choice);
        }

        /// <summary>
        /// 异步交互式创建FileFilterRule
        /// </summary>
        /// <param name="promptMessage">提示消息</param>
        /// <returns>创建的FileFilterRule，如果用户选择取消或使用默认规则则返回null</returns>
        public static async Task<FileFilterRule?> CreateInteractiveFilterRuleAsync(string promptMessage = "请选择过滤规则类型:")
        {
            return await Task.Run(() => CreateInteractiveFilterRule(promptMessage));
        }

        /// <summary>
        /// 根据选择创建FileFilterRule
        /// </summary>
        /// <param name="choice">用户选择</param>
        /// <returns>创建的FileFilterRule</returns>
        public static FileFilterRule? CreateFilterRuleByChoice(string? choice)
        {
            switch (choice)
            {
                case "1":
                    Console.WriteLine($"已选择文档文件过滤规则");
                    return FileFilterRuleFactory.CreateDocumentRule();
                case "2":
                    Console.WriteLine($"已选择图片文件过滤规则");
                    return FileFilterRuleFactory.CreateImageRule();
                case "3":
                    Console.WriteLine($"已选择视频文件过滤规则");
                    return FileFilterRuleFactory.CreateVideoRule();
                case "4":
                    Console.WriteLine($"已选择音频文件过滤规则");
                    return FileFilterRuleFactory.CreateAudioRule();
                case "5":
                    Console.WriteLine($"已选择代码文件过滤规则");
                    return FileFilterRuleFactory.CreateCodeRule();
                case "6":
                    var customRule = CreateCustomExtensionRule();
                    return customRule;
                case "7":
                    var sizeRule = CreateSizeBasedRule();
                    return sizeRule;
                case "8":
                    var wildcardRule = CreateWildcardRule();
                    return wildcardRule;
                case "9":
                    var characterRule = CreateCharacterBasedRule();
                    return characterRule;
                default:
                    Console.WriteLine($"使用默认规则（所有文件）");
                    return null;
            }
        }

        /// <summary>
        /// 显示过滤规则选项
        /// </summary>
        public static void ShowFilterRuleOptions()
        {
            Console.WriteLine("1. 文档文件 (.doc, .pdf, .txt等)");
            Console.WriteLine("2. 图片文件 (.jpg, .png, .gif等)");
            Console.WriteLine("3. 视频文件 (.mp4, .avi, .mkv等)");
            Console.WriteLine("4. 音频文件 (.mp3, .wav, .flac等)");
            Console.WriteLine("5. 代码文件 (.cs, .js, .py等)");
            Console.WriteLine("6. 自定义扩展名");
            Console.WriteLine("7. 按文件大小过滤");
            Console.WriteLine("8. 通配符模式 (*a.*, *.txt, test* 等)");
            Console.WriteLine("9. 包含特定字符 (如包含字母 a 的文件)");
            Console.Write("请选择 (1-9, 默认为全部文件): ");
        }

        /// <summary>
        /// 创建自定义扩展名过滤规则
        /// </summary>
        /// <returns>自定义扩展名过滤规则</returns>
        private static FileFilterRule? CreateCustomExtensionRule()
        {
            Console.Write("请输入允许的扩展名，用逗号分隔 (例如: .txt,.pdf,.doc): ");
            string? extInput = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(extInput))
            {
                string[] extArray = extInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var rule = FileFilterRuleFactory.CreateCustomRule(extArray);
                Console.WriteLine($"已设置扩展名过滤: {string.Join(", ", extArray)}");
                return rule;
            }
            return null;
        }

        /// <summary>
        /// 创建基于文件大小的过滤规则
        /// </summary>
        /// <returns>文件大小过滤规则</returns>
        private static FileFilterRule? CreateSizeBasedRule()
        {
            Console.Write("请输入最大文件大小 (MB): ");
            if (double.TryParse(Console.ReadLine(), out double maxMb) && maxMb > 0)
            {
                long maxSize = (long)(maxMb * 1024 * 1024);
                var rule = FileFilterRuleFactory.CreateSizeRule(0, maxSize);
                Console.WriteLine($"已选择大小限制过滤规则 (最大 {maxMb} MB)");
                return rule;
            }
            return null;
        }

        /// <summary>
        /// 创建通配符过滤规则
        /// </summary>
        /// <returns>通配符过滤规则</returns>
        private static FileFilterRule? CreateWildcardRule()
        {
            Console.Write("请输入通配符模式 (例如: *a.*, *.txt, test*, 等): ");
            string? wildcardPattern = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(wildcardPattern))
            {
                var rule = FileFilterRuleFactory.CreateWildcardRule(wildcardPattern);
                Console.WriteLine($"已选择通配符过滤规则: {wildcardPattern}");
                return rule;
            }
            return null;
        }

        /// <summary>
        /// 创建基于字符的过滤规则
        /// </summary>
        /// <returns>字符过滤规则</returns>
        private static FileFilterRule? CreateCharacterBasedRule()
        {
            Console.Write("请输入要匹配的字符 (例如: a, b, c 等): ");
            string? charInput = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(charInput) && charInput.Length == 1)
            {
                var rule = FileFilterRuleFactory.CreateCharacterInNameRule(charInput[0]);
                Console.WriteLine($"已选择字符匹配过滤规则: 包含字符 '{charInput[0]}'");
                return rule;
            }
            return null;
        }

        /// <summary>
        /// 询问用户是否要设置过滤规则
        /// </summary>
        /// <param name="prompt">提示消息</param>
        /// <returns>用户选择的结果</returns>
        public static bool AskUserForFilterRule(string prompt = "是否要设置文件过滤规则？(y/n，默认为n): ")
        {
            Console.WriteLine(prompt);
            string? userInput = Console.ReadLine();
            return !string.IsNullOrEmpty(userInput) && userInput.ToLower().StartsWith("y");
        }

        /// <summary>
        /// 交互式获取过滤规则，包括询问是否需要设置规则
        /// </summary>
        /// <param name="initialPrompt">初始提示消息</param>
        /// <param name="selectionPrompt">选择提示消息</param>
        /// <returns>用户选择的过滤规则，如果没有选择则返回null</returns>
        public static FileFilterRule? GetInteractiveFilterRule(
            string initialPrompt = "是否要设置文件过滤规则？(y/n，默认为n): ",
            string selectionPrompt = "请选择过滤规则类型:")
        {
            if (!AskUserForFilterRule(initialPrompt))
            {
                return null;
            }

            return CreateInteractiveFilterRule(selectionPrompt);
        }
    }
}