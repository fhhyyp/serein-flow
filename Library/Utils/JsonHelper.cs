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
        public static IJsonProvider Provider { get; private set; }

        /// <summary>
        /// 使用第三方包进行解析
        /// </summary>
        /// <param name="jsonPortal"></param>
        public static void UseJsonProvider(IJsonProvider jsonPortal)
        {
            JsonHelper.Provider = jsonPortal;
        }

        /// <summary>
        /// 反序列化Json文本为指定类型的对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonText"></param>
        /// <returns></returns>

        public static T Deserialize<T>(string jsonText)
        {
            return Provider.Deserialize<T>(jsonText);
        }

        /// <summary>
        /// 反序列化Json文本为指定类型的对象
        /// </summary>
        /// <param name="jsonText"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object Deserialize(string jsonText, Type type)
        {
            return Provider.Deserialize(jsonText, type);

        }

        /// <summary>
        /// 解析Json文本为IJsonToken对象
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>

        public static IJsonToken Parse(string json)
        {
            return Provider.Parse(json);
        }

        /// <summary>
        /// 尝试解析Json文本为IJsonToken对象
        /// </summary>
        /// <param name="json"></param>
        /// <param name="jsonToken"></param>
        /// <returns></returns>
        public static bool TryParse(string json, out IJsonToken jsonToken)
        {
            return Provider.TryParse(json, out jsonToken);
        }



        /// <summary>
        /// 将对象序列化为Json文本
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Serialize(object obj)
        {
            return Provider.Serialize(obj);
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
            return Provider.CreateObject(dict);
        }

        /// <summary>
        /// 创建一个Json对象，使用字典初始化
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static IJsonToken Array(IEnumerable<object> values)
        {
            return Provider.CreateArray(values);
        }

        /// <summary>
        /// 将对象转换为JsonToken
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static IJsonToken FromObject(object obj)
        {
            if (obj is System.Collections.IEnumerable && !(obj is string))
                return Provider.CreateObject(obj as IDictionary<string, object>);
            return Provider.CreateArray(obj as IEnumerable<object>);

        }
    }


}
