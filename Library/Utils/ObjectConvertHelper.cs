using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 对象转换工具类
    /// </summary>
    public static class ObjectConvertHelper
    {
       
        /// <summary>
        /// 父类转为子类
        /// </summary>
        /// <param name="parent">父类对象</param>
        /// <param name="childType">子类类型</param>
        /// <returns></returns>
        public static object ConvertParentToChild(object parent, Type childType)
        {
            var child = Activator.CreateInstance(childType);
            var parentType = parent.GetType();

            // 复制父类属性
            foreach (var prop in parentType.GetProperties())
            {
                if (prop.CanWrite)
                {
                    var value = prop.GetValue(parent);
                    childType.GetProperty(prop.Name)?.SetValue(child, value);
                }
            }
            return child;
        }


        /// <summary>
        /// 集合类型转换为Array/List 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="targetType"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static object ConvertToEnumerableType(object obj, Type targetType)
        {
            // 获取目标类型的元素类型
            Type targetElementType = targetType.IsArray
                ? targetType.GetElementType()
                : targetType.GetGenericArguments().FirstOrDefault();

            if (targetElementType == null)
                throw new InvalidOperationException("无法获取目标类型的元素类型");

            // 检查输入对象是否为集合类型
            if (obj is IEnumerable collection)
            {
                // 判断目标类型是否是数组
                if (targetType.IsArray)
                {
                    var toArrayMethod = typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(targetElementType);
                    return toArrayMethod.Invoke(null, new object[] { collection });
                }
                // 判断目标类型是否是 List<T>
                else if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var toListMethod = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(targetElementType);
                    return toListMethod.Invoke(null, new object[] { collection });
                }
                // 判断目标类型是否是 HashSet<T>
                else if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(HashSet<>))
                {
                    var toHashSetMethod = typeof(Enumerable).GetMethod("ToHashSet").MakeGenericMethod(targetElementType);
                    return toHashSetMethod.Invoke(null, new object[] { collection });
                }
                // 其他类型可以扩展类似的处理
            }

            throw new InvalidOperationException("输入对象不是集合或目标类型不支持");
        }



        /// <summary>
        /// 对象转换为对应类型
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
            return (TResult)data.ToConvertValueType(type);

        }


        /// <summary>
        /// 对象转换
        /// </summary>
        /// <param name="data"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object ToConvertValueType(this object data, Type type)
        {
            if (type.IsValueType)
            {
                if (data == null)
                {
                    return null;
                }
                else
                {
                    return ObjectConvertHelper.ValueParse(type, data);
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
            return valueStr.ToValueData<T>();
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

        /// <summary>
        /// 将字符串转换为指定类型的值对象。
        /// </summary>
        /// <param name="valueStr"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static object ToValueData(this string valueStr, Type type)
        {
            if (string.IsNullOrWhiteSpace(valueStr))
            {
                return Activator.CreateInstance(type);
            }
            object result;
            if (type.IsEnum)
            {
                if (int.TryParse(valueStr, out int numericValue))
                {
                    // 输入是数字，直接转换
                    result = Enum.ToObject(type, numericValue);
                }
                else
                {
                    // 输入是枚举名称
                    result = Enum.Parse(type, valueStr, ignoreCase: true);
                }
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
            else if (type == typeof(DateTime))
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
