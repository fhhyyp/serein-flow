using Newtonsoft.Json.Linq;
using Serein.Library.Api;

namespace Serein.Extend.NewtonsoftJson
{
    /// <summary>
    /// 基于Newtonsoft.Json的IJsonToken实现
    /// </summary>
    public sealed class NewtonsoftJsonToken : IJsonToken
    {
        private readonly JToken _token;

        public NewtonsoftJsonToken(JToken token)
        {
            _token = token ?? throw new ArgumentNullException(nameof(token));
        }

        public bool TryGetValue(string name, out IJsonToken token)
        {
            if (_token is JObject obj && obj.TryGetValue(name, out JToken value))
            {
                token = new NewtonsoftJsonToken(value);
                return true;
            }
            token = null;
            return false;
        }

        public IJsonToken? GetValue(string name)
        {
            if (_token is JObject obj && obj.TryGetValue(name, out JToken value))
            {
                return new NewtonsoftJsonToken(value);
            }
            return null;
        }

        public string GetString() => _token.Type == JTokenType.Null ? null : _token.ToString();

        public int GetInt32() => _token.Value<int>();

        public bool GetBoolean() => _token.Value<bool>();

        public bool IsNull => _token.Type == JTokenType.Null || _token.Type == JTokenType.Undefined;

        public IEnumerable<IJsonToken> EnumerateArray()
        {
            if (_token is JArray arr)
                return arr.Select(x => new NewtonsoftJsonToken(x));
            throw new InvalidOperationException("当前Token不是数组类型。");
        }

        public T ToObject<T>() => _token.ToObject<T>();

        public object ToObject(Type type) => _token.ToObject(type);

        public override string ToString() => _token.ToString();
    }
}
