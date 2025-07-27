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
        public static void UseJsonLibrary(IJsonProvider jsonPortal)
        {
            JsonHelper.provider = jsonPortal;
        }


        public static T Deserialize<T>(string jsonText)
        {
            return provider.Deserialize<T>(jsonText);
        }

        public static object Deserialize(string jsonText, Type type)
        {
            return provider.Deserialize(jsonText, type);

        }

        public static IJsonToken Parse(string json)
        {
            return provider.Parse(json);

        }

        public static string Serialize(object obj)
        {
            return provider.Serialize(obj);
        }
        public static IJsonToken Object(Action<Dictionary<string, object>> init)
        {
            var dict = new Dictionary<string, object>();
            init(dict);
            return provider.CreateObject(dict);
        }

        public static IJsonToken Array(IEnumerable<object> values)
        {
            return provider.CreateArray(values);
        }

        public static IJsonToken FromObject(object obj)
        {
            if (obj is System.Collections.IEnumerable && !(obj is string))
                return provider.CreateObject(obj as IDictionary<string, object>);
            return provider.CreateArray(obj as IEnumerable<object>);

        }
    }


}
