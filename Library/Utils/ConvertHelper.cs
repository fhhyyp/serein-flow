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


        public static TResult ToConvert<TResult>(this object data)
        {
            var type = typeof(TResult);
            if (data is null && type.IsValueType)
            {
                return default;
            }
            return (TResult)data.ToConvert(type);

        }
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



        public static T ValueParse<T>(object value) where T : struct, IComparable<T>
        {
            string valueStr = value.ToString();
            return valueStr.ToValueData<T>() ;
        }

        public static object ValueParse(Type type, object value)
        {
            string valueStr = value.ToString();
            return valueStr.ToValueData(type);
           
        }


        public static T ToValueData<T>(this string valueStr) where T : struct, IComparable<T>
        {
            if (string.IsNullOrEmpty(valueStr))
            {
                return default(T);
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
