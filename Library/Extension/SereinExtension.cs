using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 拓展方法
    /// </summary>
    public static partial class SereinExtension
    {
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


        /// <summary>
        /// 判断连接类型
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public static JunctionOfConnectionType ToConnectyionType(this JunctionType start)
        {
            if (start == JunctionType.Execute
                    || start == JunctionType.NextStep)
            {
                return JunctionOfConnectionType.Invoke;
            }
            else
            {
                return JunctionOfConnectionType.Arg;
            }
        }
        /// <summary>
        /// 判断是否运行连接
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static bool IsCanConnection(this JunctionType start,JunctionType end)
        {
            if(start == end)
            {
                return false;
            }

            var startType = start.ToConnectyionType();
            if (startType == JunctionOfConnectionType.Invoke)
            {
                return (end == JunctionType.Execute && start == JunctionType.NextStep)
                    || (start == JunctionType.Execute && end == JunctionType.NextStep);
            }
            else // if (startType == JunctionOfConnectionType.Arg)
            {
                return (end == JunctionType.ArgData && start == JunctionType.ReturnData)
                    || (start == JunctionType.ArgData && end == JunctionType.ReturnData);
            }

         
        }

    }
}
