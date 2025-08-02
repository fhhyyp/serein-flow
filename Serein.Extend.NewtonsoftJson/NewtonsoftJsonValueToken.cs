using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Serein.Library.Api.IJsonToken;

namespace Serein.Extend.NewtonsoftJson
{
    public sealed class NewtonsoftJsonValueToken : IJsonToken
    {
        public TokenType Type => TokenType.Value;

        private readonly JToken _token;

        public NewtonsoftJsonValueToken(JToken token)
        {
            _token = token ?? throw new ArgumentNullException(nameof(token));
        }

        public bool IsNull => _token.Type == JTokenType.Null || _token.Type == JTokenType.Undefined;
        public bool IsObject => false;
        public bool IsArray => false;

        

        public string GetString() => _token.Type == JTokenType.Null ? string.Empty : _token.ToString();
        public int GetInt32() => _token.Value<int>();
        public bool GetBoolean() => _token.Value<bool>();

        public IJsonToken this[object key] => throw new InvalidOperationException("不是对象/数组类型");
        public IEnumerable<IJsonToken> EnumerateArray() => throw new InvalidOperationException("不是数组类型");

        public T ToObject<T>() => _token.ToObject<T>();
        public object ToObject(Type type) => _token.ToObject(type);
        public override string ToString() => _token.ToString();

        public bool TryGetValue(string name, out IJsonToken token) => throw new InvalidOperationException("不是对象类型");

        public IJsonToken? GetValue(string name) => throw new InvalidOperationException("不是对象类型");




    }
}
