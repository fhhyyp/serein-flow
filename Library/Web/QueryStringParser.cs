using System;
using System.Collections.Generic;
using System.Text;

namespace Serein.Library.Web
{
    internal class QueryStringParser
    {
        public static Dictionary<string, string> ParseQueryString(string query)
        {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(query))
                return result;

            // 如果字符串以'?'开头，移除它
            if (query.StartsWith("?"))
                query = query.Substring(1);

            // 拆分键值对
            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                // 忽略空的键值对
                if (string.IsNullOrEmpty(pair)) continue;

                // 用等号分隔键和值
                var keyValue = pair.Split(new[] { '=' }, 2);

                var key = Uri.UnescapeDataString(keyValue[0]); // 解码键
                var value = keyValue.Length > 1 ? Uri.UnescapeDataString(keyValue[1]) : string.Empty; // 解码值

                result[key] = value; // 添加到字典中
            }

            return result;
        }
    }
}
