using System;
using FileRecord.Utils;

/// <summary>
/// 通配符规则使用示例
/// </summary>
public class WildcardRuleExample
{
    public static void DemonstrateWildcardRules()
    {
        Console.WriteLine("=== 通配符规则使用示例 ===\n");

        // 示例1: 创建 "*a.*" 规则 - 匹配文件名中包含字母 "a" 且有扩展名的文件
        Console.WriteLine("1. '*a.*' 规则 - 匹配文件名中包含字母 'a' 且有扩展名的文件:");
        var rule1 = FileFilterRuleFactory.CreateWildcardRule("*a.*");
        TestRuleWithFiles(rule1, new[] {
            "document_a.txt",    // ✓ 匹配 - 包含 'a' 且有扩展名
            "test_file.docx",    // ✗ 不匹配 - 不包含 'a'
            "data.csv",          // ✗ 不匹配 - 不包含 'a'
            "application.exe",   // ✓ 匹配 - 包含 'a' 且有扩展名
            "my_app.config",     // ✓ 匹配 - 包含 'a' 且有扩展名
            "readme.md",         // ✗ 不匹配 - 不包含 'a'
            "backup.bak",        // ✗ 不匹配 - 不包含 'a'
            "data_a.json"        // ✓ 匹配 - 包含 'a' 且有扩展名
        });

        Console.WriteLine("\n2. '*.txt' 规则 - 匹配所有 .txt 文件:");
        var rule2 = FileFilterRuleFactory.CreateWildcardRule("*.txt");
        TestRuleWithFiles(rule2, new[] {
            "document.txt",      // ✓ 匹配 - .txt 文件
            "image.png",         // ✗ 不匹配 - .png 文件
            "script.js",         // ✗ 不匹配 - .js 文件
            "readme.txt",        // ✓ 匹配 - .txt 文件
            "data.xml"           // ✗ 不匹配 - .xml 文件
        });

        Console.WriteLine("\n3. 'test*' 规则 - 匹配以 'test' 开头的文件:");
        var rule3 = FileFilterRuleFactory.CreateWildcardRule("test*");
        TestRuleWithFiles(rule3, new[] {
            "test.txt",          // ✓ 匹配 - 以 'test' 开头
            "test_data.json",    // ✓ 匹配 - 以 'test' 开头
            "my_test.doc",       // ✗ 不匹配 - 不以 'test' 开头
            "testing.pdf",       // ✓ 匹配 - 以 'test' 开头
            "example.txt"        // ✗ 不匹配 - 不以 'test' 开头
        });

        Console.WriteLine("\n4. 'data?.csv' 规则 - 匹配 'data' 加一个字符再加 '.csv' 的文件:");
        var rule4 = FileFilterRuleFactory.CreateWildcardRule("data?.csv");
        TestRuleWithFiles(rule4, new[] {
            "data1.csv",         // ✓ 匹配 - data + 一个字符 + .csv
            "data_.csv",         // ✓ 匹配 - data + 一个字符 + .csv
            "data.csv",          // ✗ 不匹配 - 缺少中间的一个字符
            "datas.csv",         // ✗ 不匹配 - 多了一个字符
            "data1.xlsx",        // ✗ 不匹配 - 扩展名不是 .csv
            "info1.csv"          // ✗ 不匹配 - 不以 'data' 开头
        });

        Console.WriteLine("\n=== 字符匹配规则示例 ===\n");
        
        // 示例5: 创建字符匹配规则
        Console.WriteLine("5. 包含字符 'a' 的规则:");
        var rule5 = FileFilterRuleFactory.CreateCharacterInNameRule('a');
        TestRuleWithFiles(rule5, new[] {
            "document.txt",      // ✓ 匹配 - 包含 'a'
            "hello.doc",         // ✗ 不匹配 - 不包含 'a'
            "application.exe",   // ✓ 匹配 - 包含 'a'
            "readme.md",         // ✗ 不匹配 - 不包含 'a'
            "data.json"          // ✓ 匹配 - 包含 'a'
        });
    }

    private static void TestRuleWithFiles(FileFilterRule rule, string[] testFiles)
    {
        Console.WriteLine($"   规则: {rule.FileNamePatterns[0]}");
        foreach (var file in testFiles)
        {
            try
            {
                // 创建一个模拟的文件信息用于测试
                var isMatch = System.Text.RegularExpressions.Regex.IsMatch(file, rule.FileNamePatterns[0]);
                var status = isMatch ? "✓" : "✗";
                Console.WriteLine($"   {status} {file}");
            }
            catch (System.Text.RegularExpressions.RegexParseException)
            { 
                Console.WriteLine($"   ! 无效的正则表达式: {file}");
            }
        }
        Console.WriteLine();
    }
}