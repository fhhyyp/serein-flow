using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Serein.Library.Api;

namespace Serein.Extend.NewtonsoftJson
{

    public enum JsonType
    {
        Default = 0,
        Web = 1,
    }
    /// <summary>
    /// 基于Newtonsoft.Json的IJsonProvider实现
    /// </summary>
    public sealed class NewtonsoftJsonProvider : IJsonProvider
    {
        private JsonSerializerSettings? settings;

        /// <summary>
        ///  基于Newtonsoft.Json的JSON门户实现
        /// </summary>
        public NewtonsoftJsonProvider()
        {
            
        }

        /// <summary>
        /// 基于Newtonsoft.Json的JSON门户实现
        /// </summary>
        /// <param name="jsonType"></param>
        public NewtonsoftJsonProvider(JsonType jsonType)
        {
            settings = jsonType switch
            {
                JsonType.Web => new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(), // 控制首字母小写
                    NullValueHandling = NullValueHandling.Ignore                     // 可选：忽略 null
                },
                _ => new JsonSerializerSettings
                {
                },
            };
        }

        public NewtonsoftJsonProvider(JsonSerializerSettings settings)
        {
            settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(), // 控制首字母小写
                NullValueHandling = NullValueHandling.Ignore                     // 可选：忽略 null
            };
            this.settings = settings;
        }

        /// <summary>
        /// 反序列化JSON文本为指定类型的对象。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonText"></param>
        /// <returns></returns>
        public T? Deserialize<T>(string jsonText)
        {
            return JsonConvert.DeserializeObject<T>(jsonText, settings);
        }

        /// <summary>
        /// 反序列化JSON文本为指定类型的对象。
        /// </summary>
        /// <param name="jsonText"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public object? Deserialize(string jsonText, Type type)
        {
            return JsonConvert.DeserializeObject(jsonText, type, settings);
        }

        /// <summary>
        /// 序列化对象为JSON文本。
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, settings);
        }

        /// <summary>
        /// 将JSON文本解析为IJsonToken对象。
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>

        public IJsonToken Parse(string json)
        {
            var token = JToken.Parse(json);
            return new NewtonsoftJsonToken(token);
        }

        /// <summary>
        /// 创建一个新的JSON对象。
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public IJsonToken CreateObject(IDictionary<string, object>? values = null)
        {
            var jobj = values != null ? JObject.FromObject(values) : new JObject();
            return new NewtonsoftJsonToken(jobj);
        }

        /// <summary>
        /// 创建一个新的JSON数组。
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>

        public IJsonToken CreateArray(IEnumerable<object>? values = null)
        {
            var jarr = values != null ? JArray.FromObject(values) : new JArray();
            return new NewtonsoftJsonToken(jarr);
        }

        /// <summary>
        /// 将对象转换为IJsonToken。
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public IJsonToken FromObject(object obj)
        {
            var token = JToken.FromObject(obj);
            return new NewtonsoftJsonToken(token);
        }
    }
}
