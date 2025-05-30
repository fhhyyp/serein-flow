using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 类型转换工具类
    /// </summary>
    public static class ConvertHelper
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
        /// 对象转JSON文本
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string ToJsonText(this object obj)
        {
            var jsonText = JsonConvert.SerializeObject(obj, Formatting.Indented);
            return jsonText;
        }

        /// <summary>
        /// JSON文本转对象
        /// </summary>
        /// <typeparam name="T">转换类型</typeparam>
        /// <param name="json">JSON文本</param>
        /// <returns></returns>
        public static T ToJsonObject<T>(this string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception)
            {
                return default(T);
            }
        }



        /// <summary>
        /// 对象转换（好像没啥用）
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public static TResult ToConvert<TResult>(this object data)
        {
            var type = typeof(TResult);
            if (data is null && type.IsValueType)
            {
                return default;
            }
            return (TResult)data.ToConvert(type);

        }


        /// <summary>
        /// 对象转换（好像没啥用）
        /// </summary>
        /// <param name="data"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object ToConvert(this object data, Type type)
        {
            if (type.IsValueType)
            {
                if (data == null)
                {
                    return Activator.CreateInstance(type);
                }
                else
                {
                    return ConvertHelper.ValueParse(type, data);
                }
            }
            return data;

        }



        /// <summary>
        /// 文本
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static T ValueParse<T>(object value) where T : struct, IComparable<T>
        {
            if (value is T data)
            {
                return data;
            }
            string valueStr = value.ToString();
            return valueStr.ToValueData<T>() ;
        }

        /// <summary>
        /// 文本转换数值
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static object ValueParse(Type type, object value)
        {
            string valueStr = value.ToString();
            return valueStr.ToValueData(type);
           
        }

        /// <summary>
        ///  文本转换值对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="valueStr"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static T ToValueData<T>(this string valueStr) where T : struct, IComparable<T>
        {
            if (string.IsNullOrEmpty(valueStr))
            {
                throw new NullReferenceException();
                //return default(T);
            }
            var type = typeof(T);
            object result;
            if (type.IsEnum)
            {
                result = Enum.Parse(type, valueStr);
            }
            else if (type == typeof(bool))
            {
                result = bool.Parse(valueStr);
            }
            else if (type == typeof(float))
            {
                result = float.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(decimal))
            {
                result = decimal.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(double))
            {
                result = double.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(sbyte))
            {
                result = sbyte.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(byte))
            {
                result = byte.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(short))
            {
                result = short.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(ushort))
            {
                result = ushort.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(int))
            {
                result = int.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(uint))
            {
                result = uint.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(long))
            {
                result = long.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(ulong))
            {
                result = ulong.Parse(valueStr, CultureInfo.InvariantCulture);
            }
#if NET6_0 || NET7_0 || NET8_0
            else if (type == typeof(nint))
            {
                result = nint.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(nuint))
            {
                result = nuint.Parse(valueStr, CultureInfo.InvariantCulture);
            }
#endif
            else
            {
                throw new ArgumentException("非预期值类型");
            }

            return (T)result;
        }
        public static object ToValueData(this string valueStr, Type type)
        {
            if (string.IsNullOrEmpty(valueStr))
            {
                return Activator.CreateInstance(type); 
            }
            object result;
            if (type.IsEnum)
            {
                result = Enum.Parse(type, valueStr);
            }
            else if (type == typeof(bool))
            {
                result = bool.Parse(valueStr);
            }
            else if (type == typeof(float))
            {
                result = float.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(decimal))
            {
                result = decimal.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(double))
            {
                result = double.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(sbyte))
            {
                result = sbyte.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(byte))
            {
                result = byte.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(short))
            {
                result = short.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(ushort))
            {
                result = ushort.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(int))
            {
                result = int.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(uint))
            {
                result = uint.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(long))
            {
                result = long.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(ulong))
            {
                result = ulong.Parse(valueStr, CultureInfo.InvariantCulture);
            }
#if NET6_0 || NET7_0 || NET8_0
            else if (type == typeof(nint))
            {
                result = nint.Parse(valueStr, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(nuint))
            {
                result = nuint.Parse(valueStr, CultureInfo.InvariantCulture);
            }
#endif
            else if(type == typeof(DateTime))
            {
                if (valueStr.Equals("now"))
                {
                    return DateTime.Now;
                }
                else if (valueStr.Equals("utcnow"))
                {
                    return DateTime.UtcNow;
                }
                return DateTime.Parse(valueStr);
            }
            else
            {
                throw new ArgumentException("非预期值类型");
            }

            return result;
        }
    }
}
