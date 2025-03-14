using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public static object ConvertParentToChild(object parent,Type childType)
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


    }
}
