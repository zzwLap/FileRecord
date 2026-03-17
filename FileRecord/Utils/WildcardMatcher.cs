using System;
using System.Text.RegularExpressions;

namespace FileRecord.Utils
{
    /// <summary>
    /// 通配符匹配工具类，统一处理通配符到正则表达式的转换和匹配
    /// 用于替换分散在各处的重复通配符处理逻辑
    /// </summary>
    public static class WildcardMatcher
    {
        /// <summary>
        /// 将通配符模式转换为正则表达式
        /// </summary>
        /// <param name="wildcard">通配符模式，如 *a.*, *.txt, test* 等</param>
        /// <returns>对应的正则表达式</returns>
        public static string ConvertWildcardToRegex(string wildcard)
        {
            if (string.IsNullOrEmpty(wildcard))
                return "^$";

            // 将通配符转换为正则表达式模式
            string regex = wildcard
                .Replace(".", "\\.")  // 转义点号
                .Replace("?", ".")   // ? 匹配任意单个字符
                .Replace("**", ".*") // ** 匹配任意路径
                .Replace("*", "[^/\\\\]*"); // * 匹配任意多个字符（不包括路径分隔符）

            return "^" + regex + "$"; // 完整匹配
        }

        /// <summary>
        /// 检查文件名是否匹配通配符模式
        /// </summary>
        /// <param name="fileName">实际文件名</param>
        /// <param name="pattern">通配符模式（支持*和?）</param>
        /// <returns>是否匹配</returns>
        public static bool IsNameMatch(string fileName, string pattern)
        {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(pattern))
                return string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);

            string regexPattern = ConvertWildcardToRegex(pattern);

            try
            {
                return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                // 如果正则表达式无效，回退到简单的字符串比较
                return fileName.Contains(pattern);
            }
        }

        /// <summary>
        /// 检查路径是否匹配通配符模式（支持**作为多级目录通配符）
        /// </summary>
        /// <param name="path">实际路径</param>
        /// <param name="pattern">通配符模式（支持*, ?, **）</param>
        /// <returns>是否匹配</returns>
        public static bool IsPathMatch(string path, string pattern)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(pattern))
                return string.Equals(path, pattern, StringComparison.OrdinalIgnoreCase);

            // 处理路径特定的通配符模式
            string regexPattern = pattern
                .Replace(".", "\\.")
                .Replace("?", ".")
                .Replace("**", ".*")  // ** 匹配任意多级目录
                .Replace("*", "[^/\\\\]*"); // * 匹配单级目录中的任意字符

            regexPattern = "^" + regexPattern + "$";

            try
            {
                return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                // 如果正则表达式无效，回退到简单的字符串比较
                return path.Contains(pattern);
            }
        }

        /// <summary>
        /// 检查字符串是否匹配任一模式
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <param name="patterns">模式数组</param>
        /// <param name="usePathMatching">是否使用路径匹配（支持**）</param>
        /// <returns>如果匹配任一模式返回true</returns>
        public static bool MatchesAnyPattern(string input, string[] patterns, bool usePathMatching = false)
        {
            if (patterns == null || patterns.Length == 0)
                return true; // 没有模式限制，默认通过

            foreach (var pattern in patterns)
            {
                bool isMatch = usePathMatching 
                    ? IsPathMatch(input, pattern) 
                    : IsNameMatch(input, pattern);

                if (isMatch)
                    return true;
            }

            return false;
        }
    }
}
