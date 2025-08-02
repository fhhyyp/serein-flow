using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.Library.Utils;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using static Serein.Library.Api.IJsonToken;

namespace Serein.Extend.NewtonsoftJson
{
    /// <summary>
    /// 基于Newtonsoft.Json的IJsonToken实现
    /// </summary>
    public sealed class NewtonsoftJsonObjectToken : IJsonToken, IDictionary<string, IJsonToken>
    {
        public TokenType Type => TokenType.Object;

        private readonly JObject _object;
        public NewtonsoftJsonObjectToken(JObject obj) => _object = obj;

        public bool IsNull => false;
        public bool IsObject => true;
        public bool IsArray => false;

        public string GetString() => _object.ToString();
        public int GetInt32() => throw new InvalidOperationException("不是值类型");
        public bool GetBoolean() => throw new InvalidOperationException("不是值类型");

        public IJsonToken this[object key] => key is string name ? this[name] : throw new InvalidOperationException("不是数组类型");

        public IJsonToken this[string key]
        {
            get => _object.TryGetValue(key, out var value) ? NewtonsoftJsonTokenFactory.FromJToken(value) : throw new KeyNotFoundException(key);
            set => _object[key] = JToken.FromObject(value.ToObject<object>());
        }

        public IEnumerable<IJsonToken> EnumerateArray() => throw new InvalidOperationException("不是数组类型");

        public T ToObject<T>() => _object.ToObject<T>();
        public object ToObject(Type type) => _object.ToObject(type);
        public override string ToString() => _object.ToString();
        public bool TryGetValue(string name, [NotNullWhen(true)] out IJsonToken? token)
        {
            if (_object.TryGetValue(name, out JToken? value))
            {
                token = NewtonsoftJsonTokenFactory.FromJToken(value);
                return true;
            }
            token = null;
            return false;
        }
        public IJsonToken? GetValue(string name)
        {
            if (_object.TryGetValue(name, out JToken? value))
            {
                var token = NewtonsoftJsonTokenFactory.FromJToken(value);
                return token;
            }
            return null;
        }

        #region  IDictionary<string, IJsonToken> 接口实现

        public ICollection<string> Keys => _object.Properties().Select(p => p.Name).ToList();

        public ICollection<IJsonToken> Values => _object.Properties().Select(p => new NewtonsoftJsonObjectToken(JObject.FromObject(p.Value)) as IJsonToken).ToList();

        public int Count => _object.Count;

        public bool IsReadOnly => false;

        public void Add(string key, IJsonToken value)
        {
            var token = JToken.FromObject(value.ToObject<object>());
            _object[key] = token;
        }


        public void Add(KeyValuePair<string, IJsonToken> item) => Add(item.Key, item.Value);

        public void Clear() => _object.RemoveAll();

        public bool ContainsKey(string key) => _object.ContainsKey(key);


        public void CopyTo(KeyValuePair<string, IJsonToken>[] array, int arrayIndex)
        {
            foreach (var prop in _object.Properties())
            {
                array[arrayIndex++] = new KeyValuePair<string, IJsonToken>(prop.Name, new NewtonsoftJsonObjectToken(JObject.FromObject(prop.Value)));
            }
        }
        public IEnumerator<KeyValuePair<string, IJsonToken>> GetEnumerator()
        {
            foreach (var prop in _object.Properties())
            {
                yield return new KeyValuePair<string, IJsonToken>(prop.Name, new NewtonsoftJsonObjectToken(JObject.FromObject(prop.Value)));
            }
        }

        public bool Remove(string key) =>   _object.Remove(key);


        public bool Remove(KeyValuePair<string, IJsonToken> item)
        {
            if (_object.TryGetValue(item.Key, out var token))
            {
                var existing = new NewtonsoftJsonObjectToken(JObject.FromObject(token));
                if (existing.Equals(item.Value))
                {
                    return _object.Remove(item.Key);
                }
            }
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Contains(KeyValuePair<string, IJsonToken> item)
        {
            if (_object.TryGetValue(item.Key, out var token))
            {
                var value = new NewtonsoftJsonObjectToken(JObject.FromObject(token));
                return value.Equals(item.Value);
            }
            return false;
        }


        #endregion

    }
}
