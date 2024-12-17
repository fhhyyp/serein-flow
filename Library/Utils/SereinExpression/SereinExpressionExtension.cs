using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils.SereinExpression
{
    internal class SereinExpressionExtension
    {
        /// <summary>
        /// 尝试获取类型
        /// </summary>
        /// <param name="context"></param>
        /// <param name="elementName"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool TryGetType(string context, out string elementName , out Type type)
        {
            int startIndex = context.IndexOf('<');
            int endIndex = context.IndexOf('>');
            if (startIndex < 0 || endIndex < 0 || startIndex > endIndex)
            {
                type = null;
                elementName = null;
                return false;
            }
            elementName = context.Substring(0,startIndex);
            type = context.Substring(startIndex + 1, endIndex - startIndex - 1).ToTypeOfString();
            return true;

        }

        /// <summary>
        /// 尝试获取下标
        /// </summary>
        /// <param name="context"></param>
        /// <param name="strIndexKey">文本形式的key/索引</param>
        /// <returns></returns>
        public static bool TryGetIndex(string context,out string elementName,  out string strIndexKey)
        {
            int startIndex = context.IndexOf('[');
            int endIndex = context.IndexOf(']');
            if (startIndex < 0 || endIndex < 0 || startIndex > endIndex)
            {
                strIndexKey = null;
                elementName = null;
                return false;
            }

            elementName = context.Substring(0,startIndex);
            strIndexKey = context.Substring(startIndex + 1, endIndex - startIndex - 1);
            return true;

        }
    }
}
