using ScriptLang.Runtime;

namespace ScriptLang.Prototype
{
    /// <summary>
    /// DateTimeValue 原型扩展方法
    /// </summary>
    [PrototypeExtension(PushThis = true, NamingFormat = NamingFormat.Js)]
    public partial class DateTimePrototype
    {
        public partial bool IsTarget(Value value)
        {
            return value.IsDateTime;
        }

        /// <summary>获取内部 DateTime 值</summary>
        private static DateTime Local(DateTimeValue dt) => dt.Value.ToLocalTime();

        [PrototypeProperty]
        [LspDoc("年 (1-9999)")]
        private static NumberValue<int> Year(DateTimeValue dt)
        {
            return NumberValueFactory.Create(Local(dt).Year);
        }

        [PrototypeProperty]
        [LspDoc("月 (1-12)")]
        private static NumberValue<int> Month(DateTimeValue dt)
        {
            return NumberValueFactory.Create(Local(dt).Month);
        }

        [PrototypeProperty]
        [LspDoc("日 (1-31)")]
        private static NumberValue<int> Day(DateTimeValue dt)
        {
            return NumberValueFactory.Create(Local(dt).Day);
        }

        [PrototypeProperty]
        [LspDoc("小时 (0-23)")]
        private static NumberValue<int> Hour(DateTimeValue dt)
        {
            return NumberValueFactory.Create(Local(dt).Hour);
        }

        [PrototypeProperty]
        [LspDoc("分钟 (0-59)")]
        private static NumberValue<int> Minute(DateTimeValue dt)
        {
            return NumberValueFactory.Create(Local(dt).Minute);
        }

        [PrototypeProperty]
        [LspDoc("秒 (0-59)")]
        private static NumberValue<int> Second(DateTimeValue dt)
        {
            return NumberValueFactory.Create(Local(dt).Second);
        }

        [PrototypeProperty]
        [LspDoc("毫秒 (0-999)")]
        private static NumberValue<int> Millisecond(DateTimeValue dt)
        {
            return NumberValueFactory.Create(Local(dt).Millisecond);
        }

        [PrototypeProperty]
        [LspDoc("Tick 计数 (100ns 单位)")]
        private static NumberValue<long> Ticks(DateTimeValue dt)
        {
            return NumberValueFactory.Create(dt.Value.Ticks);
        }

        [PrototypeProperty]
        [LspDoc("星期几 (0=周日, 1=周一, ..., 6=周六)")]
        private static NumberValue<int> DayOfWeek(DateTimeValue dt)
        {
            return NumberValueFactory.Create((int)Local(dt).DayOfWeek);
        }

        [PrototypeProperty]
        [LspDoc("一年中的第几天 (1-366)")]
        private static NumberValue<int> DayOfYear(DateTimeValue dt)
        {
            return NumberValueFactory.Create(Local(dt).DayOfYear);
        }

        [PrototypeFunction]
        [LspDoc("格式化为字符串。无参默认 yyyy/MM/dd HH:mm:ss，指定 format 则按自定义格式输出")]
        private static StringValue ToString(DateTimeValue dt, StringValue? format = null)
        {
            if (format != null)
                return StringValue.Create(Local(dt).ToString(format.Value));
            return StringValue.Create(Local(dt).ToString("yyyy/MM/dd HH:mm:ss"));
        }
    }
}
