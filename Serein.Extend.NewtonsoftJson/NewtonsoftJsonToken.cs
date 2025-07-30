using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using System.Diagnostics.CodeAnalysis;

namespace Serein.Extend.NewtonsoftJson
{
    /// <summary>
    /// 基于Newtonsoft.Json的IJsonToken实现
    /// </summary>
    public sealed class NewtonsoftJsonToken : IJsonToken
    {
        private readonly JToken _token;

        /// <summary>
        /// 使用JToken初始化一个新的NewtonsoftJsonToken实例。
        /// </summary>
        /// <param name="token"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public NewtonsoftJsonToken(JToken token)
        {
            _token = token ?? throw new ArgumentNullException(nameof(token));
        }

        /// <summary>
        /// 尝试获取指定名称的属性值。
        /// </summary>
        /// <param name="name"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public bool TryGetValue(string name, [NotNullWhen(true)] out IJsonToken? token)
        {
            if (_token is JObject obj && obj.TryGetValue(name, out JToken? value))
            {
                token = new NewtonsoftJsonToken(value);
                return true;
            }
            token = null;
            return false;
        }

        public IJsonToken? GetValue(string name)
        {
            if (_token is JObject obj && obj.TryGetValue(name, out JToken? value))
            {
                return new NewtonsoftJsonToken(value);
            }
            return null;
        }

        public string GetString() => (_token.Type == JTokenType.Null ? null : _token.ToString()) ?? string.Empty;

        public int GetInt32() => _token.Value<int>();

        public bool GetBoolean() => _token.Value<bool>();

        public bool IsNull => _token.Type == JTokenType.Null || _token.Type == JTokenType.Undefined;

        public IEnumerable<IJsonToken> EnumerateArray()
        {
            if (_token is JArray arr)
                return arr.Select(x => new NewtonsoftJsonToken(x));
            throw new InvalidOperationException("当前Token不是数组类型。");
        }

        /// <summary>
        /// 将当前JSON Token转换为指定类型的对象。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
#pragma warning disable CS8603 // 可能返回 null 引用。
        public T ToObject<T>() => _token.ToObject<T>();
#pragma warning restore CS8603 // 可能返回 null 引用。

        /// <summary>
        /// 将当前JSON Token转换为指定类型的对象。
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
#pragma warning disable CS8603 // 可能返回 null 引用。
        public object ToObject(Type type) => _token.ToObject(type);
#pragma warning restore CS8603 // 可能返回 null 引用。

        /// <summary>
        /// 返回当前JSON Token的字符串表示形式。
        /// </summary>
        /// <returns></returns>
        public override string ToString() => _token.ToString();
    }
}
