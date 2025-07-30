using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script
{
    internal static class SereinScriptExtension
    { /// <summary>
      ///  添加代码
      /// </summary>
      /// <param name="sb">字符串构建器</param>
      /// <param name="retractCount">缩进次数（4个空格）</param>
      /// <param name="code">要添加的代码</param>
      /// <returns>字符串构建器本身</returns>
        public static StringBuilder AppendCode(this StringBuilder sb,
            int retractCount = 0,
            string? code = null,
            bool isWrapping = true)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                string retract = new string(' ', retractCount * 4);
                sb.Append(retract);
                if (isWrapping)
                {
                    sb.AppendLine(code);
                }
                else
                {
                    sb.Append(code);
                }
            }
            return sb;
        }
    }
}
