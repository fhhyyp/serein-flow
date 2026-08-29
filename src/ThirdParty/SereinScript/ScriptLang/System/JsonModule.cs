using System.Text.Encodings.Web;
using System.Text.Json;
using ScriptLang.Runtime;

namespace ScriptLang.System
{
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class JsonModule : ScriptRuntimeObject<JsonModule>
    {
        private static readonly JsonSerializerOptions _indentedOptions = new() 
        { 
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        private static readonly JsonSerializerOptions _minifiedOptions = new()
        { 
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is JsonModule;

        [PrototypeFunction] 
        [LspDoc("将脚本对象序列化为 JSON 字符串\nindent=true 时格式化缩进输出")]
        public static StringValue Stringify(Value value, BoolValue? indent = null)
        {
            indent ??= BoolValue.False;
            var obj = ConvertToClrObject(value); 
            var options = indent.Value ? _indentedOptions : _minifiedOptions; 
            return StringValue.Create(JsonSerializer.Serialize(obj, options)); 
        }

        [PrototypeFunction]
        [LspDoc("将 JSON 字符串解析为脚本对象")]
        public static Value Parse(StringValue json)
        { 
            var obj = JsonSerializer.Deserialize<object>(json.Value); return ConvertToScriptValue(obj);
        }

        private static object? ConvertToClrObject(Value value)
        { 
            return value switch
            {
                NullValue => null,
                StringValue s => s.Value,
                NumberValue<int> n => n.Value,
                NumberValue<long> n => n.Value,
                NumberValue<double> n => n.Value,
                NumberValue<decimal> n => (double)n.Value,
                BoolValue b => b.Value,
                ArrayValue a => a.Elements.Select(ConvertToClrObject).ToArray(),
                ObjectValue o => o.Properties.ToDictionary(kv => kv.Key, kv => ConvertToClrObject(kv.Value)),
                DateTimeValue dt => dt.Value.ToString("O"),
                TimeSpanValue ts => ts.Value.ToString(),
                _ => value.ToString()
            };
        }

        private static Value ConvertToScriptValue(object? obj)
        {
            return obj switch 
            { 
                null => Value.Null, 
                string s => StringValue.Create(s), 
                int i => NumberValueFactory.Create(i), 
                long l => NumberValueFactory.Create(l), 
                double d => NumberValueFactory.Create(d), 
                bool b => BoolValue.Create(b),
                JsonElement element => ConvertJsonElement(element),
                object[] arr => new ArrayValue([.. arr.Select(ConvertToScriptValue)]), 
                Dictionary<string, object> dict => new ObjectValue(dict.ToDictionary(kv => kv.Key, kv => ConvertToScriptValue(kv.Value))),
                _ => StringValue.Create(obj.ToString() ?? "")
            };
        }

        private static Value ConvertJsonElement(JsonElement element)
        { 
            return element.ValueKind switch 
            { 
                JsonValueKind.Null => Value.Null, 
                JsonValueKind.String => StringValue.Create(element.GetString() ?? ""), 
                JsonValueKind.Number => NumberValueFactory.Create(element.GetDouble()), 
                JsonValueKind.True => BoolValue.True, 
                JsonValueKind.False => BoolValue.False, 
                JsonValueKind.Array => new ArrayValue([.. element.EnumerateArray().Select(ConvertJsonElement)]), 
                JsonValueKind.Object => new ObjectValue(element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value))),
                _ => StringValue.Create(element.ToString())
            };
        }
    }
}
