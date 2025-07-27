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
        private JsonSerializerSettings settings;

        public NewtonsoftJsonProvider()
        {
            
        }

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

        public T? Deserialize<T>(string jsonText)
        {
            return JsonConvert.DeserializeObject<T>(jsonText, settings);
        }

        public object? Deserialize(string jsonText, Type type)
        {
            return JsonConvert.DeserializeObject(jsonText, type, settings);
        }

        public string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, settings);
        }

        public IJsonToken Parse(string json)
        {
            var token = JToken.Parse(json);
            return new NewtonsoftJsonToken(token);
        }

        public IJsonToken CreateObject(IDictionary<string, object> values = null)
        {
            var jobj = values != null ? JObject.FromObject(values) : new JObject();
            return new NewtonsoftJsonToken(jobj);
        }

        public IJsonToken CreateArray(IEnumerable<object> values = null)
        {
            var jarr = values != null ? JArray.FromObject(values) : new JArray();
            return new NewtonsoftJsonToken(jarr);
        }

        public IJsonToken FromObject(object obj)
        {
            var token = JToken.FromObject(obj);
            return new NewtonsoftJsonToken(token);
        }
    }
}
