using ScriptLang.Runtime;

namespace ScriptLang.Prototype
{

    /// <summary>
    /// 字符串原型拓展方法
    /// </summary>
    [PrototypeExtension(PushThis = true, NamingFormat = NamingFormat.Js)]
    public partial class StringPrototype
    {
        public partial bool IsTarget(Value value)
        {
            return value.IsString;
        }
        [PrototypeProperty]
        private static NumberValue<int> Length(StringValue str)
        {
            return NumberValueFactory.Create(str.Value.Length);
        }

        [PrototypeFunction]
        private static StringValue ToUpper(StringValue str)
        {
            return StringValue.Create(str.Value.ToUpper());
        }

        [PrototypeFunction]
        private static StringValue ToLower(StringValue str)
        {
            return StringValue.Create(str.Value.ToLower());
        }

        [PrototypeFunction]
        private static StringValue Trim(StringValue str)
        {
            return StringValue.Create(str.Value.Trim());
        }

        [PrototypeFunction]
        private static ArrayValue Split(StringValue str, StringValue separator)
        {
            var parts = str.Value.Split(separator.Value);
            return new ArrayValue([.. parts.Select(p => (Value)StringValue.Create(p))]);
        }

        [PrototypeFunction]
        private static StringValue Substring(StringValue str, NumberValue<int> start, NumberValue<int>? length = null)
        {
            int len = length != null ? length.Value : str.Value.Length - start.Value;
            return StringValue.Create(str.Value.Substring(start.Value, len));
        }

        [PrototypeFunction]
        private static BoolValue Contains(StringValue str, StringValue value)
        {
            return BoolValue.Create(str.Value.Contains(value.Value));
        }
    }

}
