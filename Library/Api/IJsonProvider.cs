using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Api
{
    

    /// <summary>
    /// JSON数据交互的Token接口，允许使用不同的JSON库进行数据处理。
    /// </summary>
    public interface IJsonToken 
    {
        /// <summary>
        /// 获取当前Token的类型，可能是值、对象或数组。
        /// </summary>
        TokenType Type { get; }

        /// <summary>
        /// 获取当前Token的类型，可能是值、对象或数组。
        /// </summary>
        public enum TokenType
        {
            /// <summary>
            /// 表示一个值类型的Token，例如字符串、数字或布尔值。
            /// </summary>
            Value,
            /// <summary>
            /// 表示一个对象类型的Token，通常是一个键值对集合。
            /// </summary>
            Object,
            /// <summary>
            /// 表示一个数组类型的Token，通常是一个元素列表。
            /// </summary>
            Array,
        }
        /// <summary>
        /// 获取 Token
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        IJsonToken this[object name] { get; }


        /// <summary>
        /// 获取指定名称的属性，如果存在则返回true，并通过out参数返回对应的IJsonToken对象。
        /// </summary>
        /// <param name="name"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        bool TryGetValue(string name, out IJsonToken token);

        /// <summary>
        /// 获取指定名称的属性值，如果不存在则返回null。
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
#nullable enable
        IJsonToken? GetValue(string name);

        /// <summary>
        /// 获取当前Token的字符串值，如果是null则返回null。
        /// </summary>
        /// <returns></returns>
        string GetString();

        /// <summary>
        /// 获取当前Token的整数值，如果是null则抛出异常。
        /// </summary>
        /// <returns></returns>
        int GetInt32();

        /// <summary>
        /// 获取当前Token的布尔值，如果是null则抛出异常。
        /// </summary>
        /// <returns></returns>
        bool GetBoolean();

        /// <summary>
        /// 判断当前Token是否为null。
        /// </summary>
        bool IsNull { get; }

        /// <summary>
        /// 枚举当前Token作为数组时的所有元素，返回一个IEnumerable&lt;IJsonTokens&gt;。
        /// </summary>
        /// <returns></returns>
        IEnumerable<IJsonToken> EnumerateArray();

        /// <summary>
        /// 将当前Token转换为指定类型的对象。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T ToObject<T>();

        /// <summary>
        /// 将当前Token转换为指定类型的对象。
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        object ToObject(Type type);

        /// <summary>
        /// 将当前Token转换为字符串表示形式。
        /// </summary>
        /// <returns></returns>
        string ToString();
    }


    /// <summary>
    /// 使用Json进行数据交互的门户,允许外部使用第三方JSON库进行数据处理。
    /// </summary>
    public interface IJsonProvider
    {
        
        /// <summary>
        /// JSON文本转为指定类型
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonText"></param>
        /// <returns></returns>
        T Deserialize<T>(string jsonText);

        /// <summary>
        /// JSON文本转为指定类型
        /// </summary>
        /// <param name="jsonText"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        object Deserialize(string jsonText, Type type);

        /// <summary>
        /// 从对象转换为JSON文本
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        string Serialize(object obj);

        /// <summary>
        /// 解析为Token
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        IJsonToken Parse(string json);

        /// <summary>
        /// 尝试解析JSON文本为IJsonToken对象，如果成功则返回true，并通过out参数返回解析后的对象。
        /// </summary>
        /// <param name="json"></param>
        /// <param name="jsonToken"></param>
        /// <returns></returns>
        bool TryParse(string json, out IJsonToken jsonToken);

        /// <summary>
        /// 创建对象
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        IJsonToken CreateObject(IDictionary<string, object>? values = null);

        /// <summary>
        /// 创建数组
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        IJsonToken CreateArray(IEnumerable<object>? values = null);

        /// <summary>
        /// 将对象转换为JSON Token，自动转换为 JObject/JArray。
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        IJsonToken FromObject(object obj); 
    }
}

/*
 
using System.Text.Json;

public class SystemTextJsonToken : IJsonToken
{
    private readonly JsonElement _element;

    public SystemTextJsonToken(JsonElement element) => _element = element;

    public bool TryGetProperty(string name, out IJsonToken token)
    {
        if (_element.TryGetProperty(name, out var property))
        {
            token = new SystemTextJsonToken(property);
            return true;
        }
        token = null;
        return false;
    }

    public string GetString() => _element.ValueKind == JsonValueKind.Null ? null : _element.GetString();
    public int GetInt32() => _element.GetInt32();
    public bool GetBoolean() => _element.GetBoolean();
    public bool IsNull => _element.ValueKind == JsonValueKind.Null;

    public IEnumerable<IJsonToken> EnumerateArray()
    {
        if (_element.ValueKind == JsonValueKind.Array)
            return _element.EnumerateArray().Select(e => new SystemTextJsonToken(e));
        return Enumerable.Empty<IJsonToken>();
    }
}

public class SystemTextJsonProvider : IJsonProvider
{
    public string Serialize(object obj) => JsonSerializer.Serialize(obj);
    public T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json);

    public IJsonToken Parse(string json) => new SystemTextJsonToken(JsonDocument.Parse(json).RootElement);
}

 public T ToObject<T>()
    {
        // JsonElement -> string -> T
        return JsonSerializer.Deserialize<T>(_element.GetRawText());
    }
 
public IJsonToken CreateObject(IDictionary<string, object> values = null)
    {
        string json = values == null 
            ? "{}" 
            : JsonSerializer.Serialize(values);
        return Parse(json);
    }

    public IJsonToken CreateArray(IEnumerable<object> values = null)
    {
        string json = values == null 
            ? "[]" 
            : JsonSerializer.Serialize(values);
        return Parse(json);
    }

    public IJsonToken FromObject(object obj)
    {
        string json = JsonSerializer.Serialize(obj);
        return Parse(json);
    }

 */


/*
 
using Newtonsoft.Json.Linq;

public class NewtonsoftJsonToken : IJsonToken
{
    private readonly JToken _token;

    public NewtonsoftJsonToken(JToken token) => _token = token;

    public bool TryGetProperty(string name, out IJsonToken token)
    {
        if (_token is JObject obj && obj.TryGetValue(name, out var value))
        {
            token = new NewtonsoftJsonToken(value);
            return true;
        }
        token = null;
        return false;
    }

    public string GetString() => _token.Type == JTokenType.Null ? null : _token.ToString();
    public int GetInt32() => _token.Value<int>();
    public bool GetBoolean() => _token.Value<bool>();
    public bool IsNull => _token.Type == JTokenType.Null;

    public IEnumerable<IJsonToken> EnumerateArray()
    {
        if (_token is JArray array)
            return array.Select(x => new NewtonsoftJsonToken(x));
        return Enumerable.Empty<IJsonToken>();
    }

}

public class NewtonsoftJsonProvider : IJsonProvider
{
    public string Serialize(object obj) => JsonConvert.SerializeObject(obj);
    public T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json);

    public IJsonToken Parse(string json) => new NewtonsoftJsonToken(JToken.Parse(json));
}

 public T ToObject<T>()
    {
        return _token.ToObject<T>();
    }
 public IJsonToken CreateObject(IDictionary<string, object> values = null)
    {
        var obj = new JObject();
        if (values != null)
        {
            foreach (var kvp in values)
            {
                obj[kvp.Key] = kvp.Value == null ? JValue.CreateNull() : JToken.FromObject(kvp.Value);
            }
        }
        return new NewtonsoftJsonToken(obj);
    }

    public IJsonToken CreateArray(IEnumerable<object> values = null)
    {
        var array = values != null ? new JArray(values.Select(JToken.FromObject)) : new JArray();
        return new NewtonsoftJsonToken(array);
    }

    public IJsonToken FromObject(object obj)
    {
        if (obj == null) return new NewtonsoftJsonToken(JValue.CreateNull());
        if (obj is System.Collections.IEnumerable && !(obj is string))
            return new NewtonsoftJsonToken(JArray.FromObject(obj));
        return new NewtonsoftJsonToken(JObject.FromObject(obj));
    }
 */