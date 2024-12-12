using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Tool
{
    /// <summary>
    /// Guid替换工具类
    /// </summary>
    public class GuidReplacer
    {
        private class TrieNode
        {
            public Dictionary<char, TrieNode> Children = new();
            public string Replacement; // 替换后的值
        }

        private readonly TrieNode _root = new();

        // 构建字典树
        public void AddReplacement(string guid, string replacement)
        {
            var current = _root;
            foreach (var c in guid)
            {
                if (!current.Children.ContainsKey(c))
                {
                    current.Children[c] = new TrieNode();
                }
                current = current.Children[c];
            }
            current.Replacement = replacement;
        }

        // 替换逻辑
        public string Replace(string input)
        {
            var result = new StringBuilder();
            var current = _root;
            int i = 0;

            while (i < input.Length)
            {
                if (current.Children.ContainsKey(input[i]))
                {
                    current = current.Children[input[i]];
                    i++;

                    if (current.Replacement != null) // 找到匹配
                    {
                        result.Append(current.Replacement);
                        current = _root; // 回到根节点
                    }
                }
                else
                {
                    result.Append(input[i]);
                    current = _root; // 未匹配，回到根节点
                    i++;
                }
            }
            return result.ToString();
        }
    }

}
