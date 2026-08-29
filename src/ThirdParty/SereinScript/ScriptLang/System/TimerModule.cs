using ScriptLang.Runtime;

namespace ScriptLang.System
{


    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class TimerModule : ScriptRuntimeObject<TimerModule>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is TimerModule;

        [PrototypeFunction]
        [LspDoc("异步延迟指定毫秒数")]
        public static async Task<Value> Sleep(NumberValue<int> ms, ScriptEngine engine)
        {
            await Task.Delay(ms.Value, engine.CurrentCancellationToken);
            return Value.Null;
        }

        [PrototypeFunction]
        [LspDoc("延迟指定毫秒后执行一次回调")]
        public static ObjectValue SetTimeout(ICallable callback, NumberValue<int> ms, ScriptEngine engine)
        {
            var timer = new Timer(async _ =>
            {
                await callback.CallAsync(engine);
            }, null, ms.Value, Timeout.Infinite);
            return new TimerWrapper(timer).ToObjectValue();
        }

            [PrototypeFunction]
        [LspDoc("每隔指定毫秒重复执行回调\n返回定时器对象，可用 clearTimer 停止")]
        public static ObjectValue SetInterval(ICallable callback, NumberValue<int> ms, ScriptEngine engine)
        { 
            var timer = new Timer(async _ => { 
                await callback.CallAsync(engine); 
            }, null, ms.Value, ms.Value);
            return new TimerWrapper(timer).ToObjectValue();
        }

        [PrototypeFunction] 
        [LspDoc("清除由 setTimeout/setInterval 创建的定时器")]
        public static void ClearTimer(Value timer) 
        {
            if (timer is ObjectValue obj && obj.Properties.TryGetValue(nameof(timer), out var t)
                && t is ClrObjectValue clr && clr.Value is TimerWrapper w)
            {
                w.Dispose();
            }
            if (timer is ClrObjectValue clr1 && clr1.Value is TimerWrapper w1)
            {
                w1.stop();
            }


           /* if(timer.Properties.TryGetValue("timer", out var t)
                && t is ClrObjectValue clr && clr.Value is TimerWrapper w)
            {
                w.Dispose();
            }*/

            /*if (timer.Value is TimerWrapper w) 
                w.Dispose();*/
        }
    }

    internal class TimerWrapper(Timer timer) : IDisposable
    {
        private Timer? timer = timer;
        public void Dispose()
        {
            timer?.Dispose();
            timer = null;
            _tcs.SetResult();
        }

        private TaskCompletionSource _tcs = new();

        public async Task wait()
        {
            await _tcs.Task;
        }

        public void stop()
        {
            this.Dispose();
        }

        public ObjectValue ToObjectValue()
        {
            var wrapper = this;
            return new ObjectValue(new()
            {
                [nameof(timer)] = new ClrObjectValue(wrapper),
                [nameof(wait)] = new FunctionValue(nameof(wait), wrapper.wait),
                [nameof(stop)] = new FunctionValue(nameof(stop), wrapper.stop)
            });
        }
    }
}
