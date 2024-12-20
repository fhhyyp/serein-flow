using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 字符串字面量节点
    /// </summary>
    public class StringNode : ASTNode
    {
        public string Value { get; }

        public StringNode(string input)
        {
            // 使用 StringBuilder 来构建输出
            StringBuilder output = new StringBuilder(input.Length);

            for (int i = 0; i < input.Length; i++)
            {
                if (i < input.Length - 1 && input[i] == '\\')  // 找到反斜杠
                {
                    char nextChar = input[i + 1];

                    // 处理转义符
                    switch (nextChar)
                    {
                        case 'r':
                            output.Append('\r');
                            i++;  // 跳过 'r'
                            break;
                        case 'n':
                            output.Append('\n');
                            i++;  // 跳过 'n'
                            break;
                        case 't':
                            output.Append('\t');
                            i++;  // 跳过 't'
                            break;
                        case '\\':  // 字面量反斜杠
                            output.Append('\\');
                            i++;  // 跳过第二个 '\\'
                            break;
                        default:
                            output.Append(input[i]);  // 不是转义符，保留反斜杠
                            break;
                    }
                }
                else
                {
                    output.Append(input[i]);  // 其他字符直接添加
                }
            }
            Value = output.ToString();
        }
    }


}
