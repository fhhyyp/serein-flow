using ScriptLang.Runtime;

namespace ScriptLang.Prototype
{
    /// <summary>
    /// TimeSpanValue 原型扩展方法
    /// </summary>
    [PrototypeExtension(PushThis = true, NamingFormat = NamingFormat.Js)]
    public partial class TimeSpanPrototype
    {
        public partial bool IsTarget(Value value)
        {
            return value.IsTimeSpan;
        }

        // ===== 整数部分属性 =====

        [PrototypeProperty]
        [LspDoc("天数部分（整数）")]
        private static NumberValue<int> Days(TimeSpanValue ts)
        {
            return NumberValueFactory.Create(ts.Value.Days);
        }

        [PrototypeProperty]
        [LspDoc("小时部分 (0-23)")]
        private static NumberValue<int> Hours(TimeSpanValue ts)
        {
            return NumberValueFactory.Create(ts.Value.Hours);
        }

        [PrototypeProperty]
        [LspDoc("分钟部分 (0-59)")]
        private static NumberValue<int> Minutes(TimeSpanValue ts)
        {
            return NumberValueFactory.Create(ts.Value.Minutes);
        }

        [PrototypeProperty]
        [LspDoc("秒部分 (0-59)")]
        private static NumberValue<int> Seconds(TimeSpanValue ts)
        {
            return NumberValueFactory.Create(ts.Value.Seconds);
        }

        [PrototypeProperty]
        [LspDoc("毫秒部分 (0-999)")]
        private static NumberValue<int> Milliseconds(TimeSpanValue ts)
        {
            return NumberValueFactory.Create(ts.Value.Milliseconds);
        }

        // ===== 总量属性（浮点） =====

        [PrototypeProperty]
        [LspDoc("总天数（含小数）")]
        private static NumberValue<double> TotalDays(TimeSpanValue ts)
        {
            return NumberValueFactory.Create(ts.Value.TotalDays);
        }

        [PrototypeProperty]
        [LspDoc("总小时数（含小数）")]
        private static NumberValue<double> TotalHours(TimeSpanValue ts)
        {
            return NumberValueFactory.Create(ts.Value.TotalHours);
        }

        [PrototypeProperty]
        [LspDoc("总分钟数（含小数）")]
        private static NumberValue<double> TotalMinutes(TimeSpanValue ts)
        {
            return NumberValueFactory.Create(ts.Value.TotalMinutes);
        }

        [PrototypeProperty]
        [LspDoc("总秒数（含小数）")]
        private static NumberValue<double> TotalSeconds(TimeSpanValue ts)
        {
            return NumberValueFactory.Create(ts.Value.TotalSeconds);
        }

        [PrototypeProperty]
        [LspDoc("总毫秒数（含小数）")]
        private static NumberValue<double> TotalMilliseconds(TimeSpanValue ts)
        {
            return NumberValueFactory.Create(ts.Value.TotalMilliseconds);
        }

        [PrototypeProperty]
        [LspDoc("Tick 计数 (100ns 单位)")]
        private static NumberValue<long> Ticks(TimeSpanValue ts)
        {
            return NumberValueFactory.Create(ts.Value.Ticks);
        }

        // ===== 方法 =====

        [PrototypeFunction]
        [LspDoc("格式化为字符串。无参默认 TimeSpan 标准格式，指定 format 则按自定义格式输出")]
        private static StringValue ToString(TimeSpanValue ts, StringValue? format = null)
        {
            if (format != null)
                return StringValue.Create(ts.Value.ToString(format.Value));
            return StringValue.Create(ts.Value.ToString());
        }
    }
}
