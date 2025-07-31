using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 类型转换工具类
    /// </summary>
    public static class TypeHelper
    {


        public static string GetFriendlyName(this Type type,bool isFullName = true)
        {
            if (type.IsGenericType)
            {
                var builder = new StringBuilder();
                string typeName = isFullName? type.FullName : type.Name;
                int backtickIndex = typeName.IndexOf('`');
                if (backtickIndex > 0)
                {
                    typeName = typeName.Substring(0, backtickIndex);
                }

                builder.Append(typeName);
                builder.Append('<');

                Type[] genericArgs = type.GetGenericArguments();
                for (int i = 0; i < genericArgs.Length; i++)
                {
                    builder.Append(genericArgs[i].GetFriendlyName(isFullName));
                    if (i < genericArgs.Length - 1)
                        builder.Append(", ");
                }

                builder.Append('>');
                return builder.ToString();
            }
            else if (type.IsArray)
            {
                return $"{type.GetElementType().GetFriendlyName()}[]";
            }
            else
            {
                return TypeMap.TryGetValue(type, out var alias) ? alias : isFullName ? type.FullName : type.Name; ;
            }
        }

        private static readonly Dictionary<Type, string> TypeMap = new Dictionary<Type, string>
        {
            [typeof(int)] = "int",
            [typeof(string)] = "string",
            [typeof(bool)] = "bool",
            [typeof(void)] = "void",
            [typeof(object)] = "object",
            [typeof(double)] = "double",
            [typeof(float)] = "float",
            [typeof(long)] = "long",
            [typeof(byte)] = "byte",
            [typeof(char)] = "char",
            [typeof(decimal)] = "decimal",
            [typeof(short)] = "short",
            [typeof(uint)] = "uint",
            [typeof(ulong)] = "ulong",
            [typeof(ushort)] = "ushort",
            [typeof(sbyte)] = "sbyte",
        };



        /// <summary>
        /// 字面量转为对应类型
        /// </summary>
        /// <param name="valueStr"></param>
        /// <returns></returns>
        public static Type ToTypeOfString(this string valueStr)
        {
            if (valueStr.IndexOf('.') != -1)
            {
                // 通过指定的类型名称获取类型
                return Type.GetType(valueStr);
            }


            if (valueStr.Equals("bool", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(bool);
            }
            #region 整数型
            else if (valueStr.Equals("sbyte", StringComparison.OrdinalIgnoreCase)
                    || valueStr.Equals(nameof(SByte), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(SByte);
            }
            else if (valueStr.Equals("short", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(Int16), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Int16);
            }
            else if (valueStr.Equals("int", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(Int32), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Int32);
            }
            else if (valueStr.Equals("long", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(Int64), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Int64);
            }

            else if (valueStr.Equals("byte", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(Byte), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Byte);
            }
            else if (valueStr.Equals("ushort", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(UInt16), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(UInt16);
            }
            else if (valueStr.Equals("uint", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(UInt32), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(UInt32);
            }
            else if (valueStr.Equals("ulong", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(UInt64), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(UInt64);
            }
            #endregion

            #region 浮点型
            else if (valueStr.Equals("float", StringComparison.OrdinalIgnoreCase)
                        || valueStr.Equals(nameof(Single), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Single);
            }
            else if (valueStr.Equals("double", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(Double), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Double);
            }
            #endregion

            #region 小数型

            else if (valueStr.Equals("decimal", StringComparison.OrdinalIgnoreCase)
                    || valueStr.Equals(nameof(Decimal), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Decimal);
            }
            #endregion

            #region 其他常见的类型
            else if (valueStr.Equals(nameof(DateTime), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(DateTime);
            }

            else if (valueStr.Equals(nameof(String), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(String);
            }
            #endregion

            else
            {
                throw new ArgumentException($"无法解析的字面量类型[{valueStr}]");
            }
        }



      
       
    }
}
