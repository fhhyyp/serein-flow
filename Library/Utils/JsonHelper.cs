using Serein.Library.Api;
using System;
using System.Collections.Generic;

namespace Serein.Library.Utils
{
    /// <summary>
    /// Json门户类，需要你提供实现
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// Json门户类，需要你提供实现
        /// </summary>
        private static IJsonProvider provider;

        /// <summary>
        /// 使用第三方包进行解析
        /// </summary>
        /// <param name="jsonPortal"></param>
        public static void UseJsonProvider(IJsonProvider jsonPortal)
        {
            JsonHelper.provider = jsonPortal;
        }

        /// <summary>
        /// 反序列化Json文本为指定类型的对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonText"></param>
        /// <returns></returns>

        public static T Deserialize<T>(string jsonText)
        {
            return provider.Deserialize<T>(jsonText);
        }

        /// <summary>
        /// 反序列化Json文本为指定类型的对象
        /// </summary>
        /// <param name="jsonText"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object Deserialize(string jsonText, Type type)
        {
            return provider.Deserialize(jsonText, type);

        }

        /// <summary>
        /// 解析Json文本为IJsonToken对象
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>

        public static IJsonToken Parse(string json)
        {
            return provider.Parse(json);

        }

        /// <summary>
        /// 将对象序列化为Json文本
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Serialize(object obj)
        {
            return provider.Serialize(obj);
        }

        /// <summary>
        /// 创建一个Json对象，使用字典初始化
        /// </summary>
        /// <param name="init"></param>
        /// <returns></returns>
        public static IJsonToken Object(Action<Dictionary<string, object>> init)
        {
            var dict = new Dictionary<string, object>();
            init(dict);
            return provider.CreateObject(dict);
        }

        /// <summary>
        /// 创建一个Json对象，使用字典初始化
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static IJsonToken Array(IEnumerable<object> values)
        {
            return provider.CreateArray(values);
        }

        /// <summary>
        /// 将对象转换为JsonToken
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static IJsonToken FromObject(object obj)
        {
            if (obj is System.Collections.IEnumerable && !(obj is string))
                return provider.CreateObject(obj as IDictionary<string, object>);
            return provider.CreateArray(obj as IEnumerable<object>);

        }
    }


}
