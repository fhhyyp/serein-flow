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
    public sealed class NewtonsoftJsonArrayToken : IJsonToken, IList<IJsonToken>
    {
        public TokenType Type => TokenType.Array;

        private readonly JArray _array;
        public NewtonsoftJsonArrayToken(JArray array) => _array = array;

        public bool IsNull => false;
        public bool IsObject => false;
        public bool IsArray => true;

        public int Count => _array.Count;

        public bool IsReadOnly => _array.IsReadOnly;


        public string GetString() => _array.ToString();
        public int GetInt32() => throw new InvalidOperationException("不是值类型");
        public bool GetBoolean() => throw new InvalidOperationException("不是值类型");

        public IJsonToken this[object key] =>  key is int index ? this[index] : throw new InvalidOperationException("不是对象类型");

        public IJsonToken this[int index]
        {
            get => NewtonsoftJsonTokenFactory.FromJToken(_array[index]);
            set => _array[index] = JToken.FromObject(value.ToObject<object>());
        }


        public IEnumerable<IJsonToken> EnumerateArray()
        {
            foreach (var t in _array)
                yield return NewtonsoftJsonTokenFactory.FromJToken(t);
        }

        public T ToObject<T>() =>  throw new InvalidOperationException("不是对象类型");
        public object ToObject(Type type) =>  throw new InvalidOperationException("不是对象类型");
        public override string ToString() => _array.ToString();

        public bool TryGetValue(string name, out IJsonToken token) => throw new InvalidOperationException("不是对象类型");
        public IJsonToken? GetValue(string name) => throw new InvalidOperationException("不是对象类型");

        public int IndexOf(IJsonToken item)
        {
            var jt = JToken.FromObject(item.ToObject<object>());
            for (int i = 0; i < _array.Count; i++)
            {
                if (JToken.DeepEquals(_array[i], jt))
                    return i;
            }
            return -1;
        }


        public void Insert(int index, IJsonToken item)
        {
            _array.Insert(index, JToken.FromObject(item.ToObject<object>()));
        }

        public void RemoveAt(int index)
        {
            _array.RemoveAt(index);
        }

        public void Add(IJsonToken item)
        {
            _array.Add(JToken.FromObject(item.ToObject<object>()));
        }

        public void Clear()
        {
            _array.Clear();
        }

        public bool Contains(IJsonToken item)
        {
            var jt = JToken.FromObject(item.ToObject<object>());
            return _array.Any(x => JToken.DeepEquals(x, jt));
        }

        public void CopyTo(IJsonToken[] array, int arrayIndex)
        {
            foreach (var item in _array)
            {
                array[arrayIndex++] = NewtonsoftJsonTokenFactory.FromJToken(item);
            }
        }

        public bool Remove(IJsonToken item)
        {
            int index = IndexOf(item);
            if (index >= 0)
            {
                _array.RemoveAt(index);
                return true;
            }
            return false;
        }

        public IEnumerator<IJsonToken> GetEnumerator()
        {
            foreach (var item in _array)
                yield return NewtonsoftJsonTokenFactory.FromJToken(item);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

    }
}
