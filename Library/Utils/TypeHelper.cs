using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 类型转换工具类
    /// </summary>
    public static class TypeHelper
    {

        /// <summary>
        /// 对于泛型类型以友好格式显示其文本值
        /// </summary>
        /// <param name="type"></param>
        /// <param name="isFullName"></param>
        /// <returns></returns>
        public static string GetFriendlyName(this Type type, bool isFullName = true)
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
               
                if (isFullName)
                {
                    return type.FullName;
                }
                else
                {
                    return type.Name;
                }
                //return TypeMap.TryGetValue(type, out var alias) ? alias : isFullName ? type.FullName : type.Name; ;
            }
        }

        /*private static readonly Dictionary<Type, string> TypeMap = new Dictionary<Type, string>
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
        };*/


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

            #region 常见值类型

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


            #endregion

            #region Array数组类型
            if (valueStr.Equals("bool" + "[]", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(bool[]);
            }
            #region 整数型
            else if (valueStr.Equals("sbyte" + "[]", StringComparison.OrdinalIgnoreCase)
                    || valueStr.Equals(nameof(SByte) + "[]", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(SByte[]);
            }
            else if (valueStr.Equals("short" + "[]", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(Int16) + "[]", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Int16[]);
            }
            else if (valueStr.Equals("int" + "[]", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(Int32) + "[]", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Int32[]);
            }
            else if (valueStr.Equals("long" + "[]", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(Int64) + "[]", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Int64[]);
            }

            else if (valueStr.Equals("byte" + "[]", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(Byte) + "[]", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Byte[]);
            }
            else if (valueStr.Equals("ushort" + "[]", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(UInt16) + "[]", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(UInt16[]);
            }
            else if (valueStr.Equals("uint" + "[]", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(UInt32) + "[]", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(UInt32[]);
            }
            else if (valueStr.Equals("ulong" + "[]", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(UInt64) + "[]", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(UInt64[]);
            }
            #endregion

            #region 浮点型
            else if (valueStr.Equals("float" + "[]", StringComparison.OrdinalIgnoreCase)
                        || valueStr.Equals(nameof(Single) + "[]", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Single[]);
            }
            else if (valueStr.Equals("double" + "[]", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(Double) + "[]", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Double[]);
            }
            #endregion

            #region 小数型

            else if (valueStr.Equals("decimal" + "[]", StringComparison.OrdinalIgnoreCase)
                    || valueStr.Equals(nameof(Decimal) + "[]", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Decimal[]);
            }
            #endregion

            #region 其他常见的类型
            else if (valueStr.Equals(nameof(String) + "[]", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(String[]);
            }
            #endregion
            #endregion

            #region List<> 数组类型
            if (valueStr.Equals("list<bool>", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(List<bool>);
            }

            #region 整数型
            else if (valueStr.Equals("list<sbyte>", StringComparison.OrdinalIgnoreCase)
                    || valueStr.Equals(nameof(List<SByte>), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(List<SByte>);
            }
            else if (valueStr.Equals("list<short>", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(List<Int16>), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(List<Int16>);
            }
            else if (valueStr.Equals("list<int>", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(List<Int32>), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(List<Int32>);
            }
            else if (valueStr.Equals("list<long>", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(List<Int64>), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(List<Int64>);
            }

            else if (valueStr.Equals("list<byte>", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(List<Byte>), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(List<Byte>);
            }
            else if (valueStr.Equals("list<ushort>", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(List<UInt16>), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(List<UInt16>);
            }
            else if (valueStr.Equals("list<uint>", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(List<UInt32>), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(List<UInt32>);
            }
            else if (valueStr.Equals("list<ulong>", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(List<UInt64>), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(List<UInt64>);
            }
            #endregion

            #region 浮点型
            else if (valueStr.Equals("list<float>", StringComparison.OrdinalIgnoreCase)
                        || valueStr.Equals(nameof(List<Single>), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(List<Single>);
            }
            else if (valueStr.Equals("list<double>", StringComparison.OrdinalIgnoreCase)
                || valueStr.Equals(nameof(List<Double>), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(List<Double>);
            }
            #endregion

            #region 小数型

            else if (valueStr.Equals("list<decimal>", StringComparison.OrdinalIgnoreCase)
                    || valueStr.Equals(nameof(List<Decimal>), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(List<Decimal>);
            }
            #endregion

            #region 其他常见的类型
            
            else if (valueStr.Equals(nameof(List<String>), StringComparison.OrdinalIgnoreCase))
            {
                return typeof(List<String>);
            }
            #endregion

            #endregion

            else
            {
                throw new ArgumentException($"无法解析的字面量类型[{valueStr}]");
            }
        }


        /// <summary>
        /// 发掘类型中的基类
        /// </summary>
        /// <param name="types"></param>
        /// <returns></returns>
        public static Type? FindCommonBaseType(Type[] types)
        {
            if (types.Length == 0)
                return null;

            Type? baseType = types[0];

            foreach (var type in types.Skip(1))
            {
                baseType = FindCommonBaseType(baseType, type);
                if (baseType == typeof(object))
                    break;
            }

            return baseType;
        }

        /// <summary>
        /// 查找父类
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static Type FindCommonBaseType(Type a, Type b)
        {
            // 如果相等，直接返回
            if (a == b) return a;

            // 向上查找父类链，找第一个 b.IsAssignableFrom(base)
            var current = a;
            while (current != null && current != typeof(object))
            {
                if (current.IsAssignableFrom(b))
                    return current;

                current = current.BaseType!;
            }

            return typeof(object);
        }


    }
}
