using System;
using FileRecord.Utils;

namespace FileRecord.Tests
{
    /// <summary>
    /// 自定义过滤功能测试类
    /// </summary>
    public class CustomFilterTest
    {
        /// <summary>
        /// 测试自定义过滤功能
        /// </summary>
        public static void TestCustomFiltering()
        {
            Console.WriteLine("开始测试自定义过滤功能...");
            
            // 测试1: 自定义过滤函数拒绝文件
            TestCustomFilterReject();
            
            // 测试2: 自定义过滤函数允许文件
            TestCustomFilterAllow();
            
            // 测试3: 自定义过滤函数与其它过滤规则协同工作
            TestCustomFilterWithOtherRules();
            
            // 测试4: 自定义过滤函数结合大小限制
            TestCustomFilterWithSizeLimit();
            
            Console.WriteLine("自定义过滤功能测试完成！");
        }

        /// <summary>
        /// 测试自定义过滤函数拒绝文件
        /// </summary>
        private static void TestCustomFilterReject()
        {
            Console.WriteLine("测试1: 自定义过滤函数拒绝文件...");
            
            var rule = new FileFilterRule
            {
                CustomFilterFunction = (filePath) => !filePath.Contains("forbidden")
            };

            // 文件路径包含forbidden，应该被拒绝
            bool result1 = rule.IsFileAllowed("test_forbidden_file.txt", 100);
            Console.WriteLine($"  包含'forbidden'的文件被拒绝: {!result1} (期望: True)");

            // 文件路径不包含forbidden，应该被允许（继续检查其他规则）
            bool result2 = rule.IsFileAllowed("test_allowed_file.txt", 100);
            Console.WriteLine($"  不包含'forbidden'的文件被允许: {result2} (期望: True)");
        }

        /// <summary>
        /// 测试自定义过滤函数允许文件
        /// </summary>
        private static void TestCustomFilterAllow()
        {
            Console.WriteLine("测试2: 自定义过滤函数允许文件...");
            
            var rule = new FileFilterRule
            {
                CustomFilterFunction = (filePath) => filePath.Length < 50  // 路径长度小于50字符
            };

            // 短路径应该被允许
            bool result1 = rule.IsFileAllowed("short_path.txt", 100);
            Console.WriteLine($"  短路径文件被允许: {result1} (期望: True)");

            // 长路径应该被拒绝
            string longPath = new string('a', 60) + ".txt";
            bool result2 = rule.IsFileAllowed(longPath, 100);
            Console.WriteLine($"  长路径文件被拒绝: {!result2} (期望: True)");
        }

        /// <summary>
        /// 测试自定义过滤函数与其它过滤规则协同工作
        /// </summary>
        private static void TestCustomFilterWithOtherRules()
        {
            Console.WriteLine("测试3: 自定义过滤函数与其它过滤规则协同工作...");
            
            var rule = new FileFilterRule
            {
                AllowedExtensions = new[] { ".txt", ".pdf" },
                CustomFilterFunction = (filePath) => !filePath.Contains("temp")  // 不包含temp
            };

            // 文件既满足自定义过滤条件又满足扩展名条件
            bool result1 = rule.IsFileAllowed("document.txt", 100);
            Console.WriteLine($"  符合所有条件的文件被允许: {result1} (期望: True)");

            // 文件满足扩展名条件但不满足自定义条件
            bool result2 = rule.IsFileAllowed("temporary.txt", 100);
            Console.WriteLine($"  满足扩展名但不满足自定义条件的文件被拒绝: {!result2} (期望: True)");

            // 文件满足自定义条件但不满足扩展名条件
            bool result3 = rule.IsFileAllowed("document.docx", 100);
            Console.WriteLine($"  满足自定义但不满足扩展名条件的文件被拒绝: {!result3} (期望: True)");

            // 文件既不满足自定义条件也不满足扩展名条件
            bool result4 = rule.IsFileAllowed("temporary.docx", 100);
            Console.WriteLine($"  不符合任一条件的文件被拒绝: {!result4} (期望: True)");
        }

        /// <summary>
        /// 测试自定义过滤函数结合大小限制
        /// </summary>
        private static void TestCustomFilterWithSizeLimit()
        {
            Console.WriteLine("测试4: 自定义过滤函数结合大小限制...");
            
            var rule = new FileFilterRule
            {
                MinFileSize = 50,
                MaxFileSize = 1000,
                CustomFilterFunction = (filePath) => filePath.EndsWith(".txt")
            };

            // 文件满足自定义条件但大小不符合
            bool result1 = rule.IsFileAllowed("test.txt", 10);
            Console.WriteLine($"  满足自定义条件但大小不符合的文件被拒绝: {!result1} (期望: True)");

            // 文件满足自定义条件且大小符合
            bool result2 = rule.IsFileAllowed("test.txt", 100);
            Console.WriteLine($"  满足所有条件的文件被允许: {result2} (期望: True)");

            // 文件不满足自定义条件但大小符合
            bool result3 = rule.IsFileAllowed("test.pdf", 100);
            Console.WriteLine($"  不满足自定义条件的文件被拒绝: {!result3} (期望: True)");
        }
    }
}