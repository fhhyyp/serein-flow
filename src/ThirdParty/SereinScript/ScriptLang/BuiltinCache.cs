using System.Diagnostics;
using System.Globalization;
using System.Runtime.Serialization;
using ScriptLang.Runtime;

namespace ScriptLang
{


    public static class BuiltinCache
    {
        private static readonly FunctionValue debug = new FunctionValue(nameof(debug),
            static (List<Value> args) => Console.WriteLine($"debug:: {string.Join(", ", args)}"));

        private static readonly FunctionValue now = new(nameof(now),
            static () => new DateTimeValue(DateTime.Now));

        private static readonly FunctionValue date = new(nameof(date),
            static (List<Value> args) =>
            {
                if (args.Count != 1 || args[0] is not StringValue s)
                    throw new RuntimeException("date() 期望 1 个字符串参数 (ISO 8601 格式)");
                if (DateTime.TryParse(s.Value, null, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out var dt))
                    return new DateTimeValue(dt); // 无时区标记→假定本地时间；有时区偏移→转为UTC
                throw new RuntimeException($"无法解析日期字符串: '{s.Value}'");
            });

        private static readonly FunctionValue timespan = new(nameof(timespan),
            static (List<Value> args) =>
            {
                if (args.Count != 2 || !args[0].IsNumber || args[1] is not StringValue unit)
                    throw new RuntimeException("timespan() 期望 (NumberValue, StringValue unit)");

                double amount = args[0].As<double>();
                TimeSpan ts = unit.Value switch
                {
                    "days" => TimeSpan.FromDays(amount),
                    "hours" => TimeSpan.FromHours(amount),
                    "minutes" => TimeSpan.FromMinutes(amount),
                    "seconds" => TimeSpan.FromSeconds(amount),
                    "milliseconds" => TimeSpan.FromMilliseconds(amount),
                    "ticks" => TimeSpan.FromTicks((long)amount),
                    _ => throw new RuntimeException($"不支持的时间单位: '{unit.Value}'，支持: days/hours/minutes/seconds/milliseconds/ticks")
                };
                return new TimeSpanValue(ts);
            });

        #region 异常处理函数
        private static readonly FunctionValue @try = new(nameof(@try), static async (env, args) =>
        {
            if (args.Count is < 1 or > 3)
                throw new RuntimeException("try() 期望 1 个或 2 个或 3 个参数");

            if (args[0] is not ICallable callable)
                throw new RuntimeException("try() 第 1 个参数期望 lambda 函数");

            try
            {
                var result = await callable.CallAsync(env);
                return TryWrapper.Succeed(result);
            }
            catch (Exception ex)
            {
                // 处理错误回调（如果有第2个参数）
                if (args.Count == 2)
                {
                    if (args[1] is ICallable catchBackcall)
                    {
                        await TryErrorCallback(catchBackcall, env, ex);
                    }
                    else
                    {
                        throw new RuntimeException("tryCall() 第 2 个参数期望 lambda 函数");
                    }
                }

                return TryWrapper.Error(ex);
            }
            finally
            {
                // 处理错误回调（如果有第2个参数）
                if (args.Count == 3)
                {
                    if (args[2] is ICallable finallyBackcall)
                    {
                        await FinallyCallback(finallyBackcall, env);
                    }
                    else
                    {
                        throw new RuntimeException("tryCall() 第 3 个参数期望 lambda 函数");
                    }
                }
            }
            static async Task TryErrorCallback(ICallable callback, ScriptEngine env, Exception ex)
            {
                try
                {
                    await callback.CallAsync(env, new ClrObjectValue(ex));
                }
                catch (Exception callbackEx)
                {
                    ScriptLog.Error($"处理 'tryCall' catch 时发生异常：{callbackEx}");
                }
            }
            static async Task FinallyCallback(ICallable callback, ScriptEngine env)
            {
                try
                {
                    await callback.CallAsync(env);
                }
                catch (Exception callbackEx)
                {
                    ScriptLog.Error($"处理 'tryCall' finally 时发生异常：{callbackEx}");
                }
            }
        });
        private static class TryWrapper
        {
            private static ObjectValue Result(bool ok, Value? result = null, string? message = null, string? stack = null)
            {
                var propertys = new Dictionary<string, Value>(4)
            {
                {nameof(ok), BoolValue.Create(ok)},
                {nameof(result), result ?? Value.Null},
                {nameof(message), StringValue.Create(message)},
                {nameof(stack), StringValue.Create(stack)},
            };
                return new ObjectValue(propertys);
            }

            internal static ObjectValue Succeed(Value value) => Result(true, value);
            internal static ObjectValue Error(Exception ex) => Result(false, message: ex.Message, stack: ex.StackTrace);
        } 
        #endregion

        /*private static readonly FunctionValue nowtime = new(nameof(nowtime), static (args) =>
        {
            return StringValue.Create(DateTime.Now.ToString());
        });*/

        private static readonly FunctionValue sleep = new(nameof(sleep), static async (args) =>
        {
            if (args.Count != 1 || !args[0].IsNumber_Int)
                throw new RuntimeException("sleep() 期望 1 个参数");

            var index = args[0].As<int>();
            await Task.Delay(index);
        });

        private static readonly FunctionValue @typeof = new(nameof(@typeof), static (args) =>
        {
            if (args.Count != 1)
                throw new RuntimeException("typeof() 期望 1 个参数");

            var value = args[0];

            string typeStr = value switch
            {
                NumberValue<byte> => "byte",
                NumberValue<short> => "short",
                NumberValue<int> => "int",
                NumberValue<long> => "long",
                NumberValue<float> => "float",
                NumberValue<double> => "double",
                NumberValue<decimal> => "decimal",

                StringValue => "string",
                BoolValue => "boolean",
                ArrayValue => "array",
                ObjectValue => "object",
                FunctionValue => "function",
                NullValue => "null",
                ClrObjectValue => "clrObject",
                ClrMethodValue => "clrMethod",
                CompiledFunctionValue => "compiledFunction",
                MutableNumber => "mutableNumber",
                RangeIterator => "rangeIterator",
                DateTimeValue => "datetime",
                TimeSpanValue => "timespan",
                _ => "unknown"
            };

            return StringValue.Create(typeStr);
        });

        private static readonly FunctionValue print = new(nameof(print), static args =>
        {
            foreach (var arg in args)
            {
                Console.Write(arg?.AsString() + " ");
                Debug.Write(arg?.AsString() + " ");
            }
            Console.WriteLine();
            Debug.WriteLine("");
        });

        private static readonly FunctionValue range = new(nameof(range), static args =>
        {
            if (args.Count == 1)
            {
                int end = args[0].As<int>();
                return new RangeIterator(0, end);
            }
            else if (args.Count == 2)
            {
                int start = args[0].As<int>();
                int end = args[1].As<int>();
                return new RangeIterator(start, end);
            }
            throw new RuntimeException("range() 期望 1 或 2 个参数");
        });

        private static readonly FunctionValue len = new(nameof(len), static args =>
        {
            if (args.Count != 1)
                throw new RuntimeException("len() 期望 1 个参数");
            var lenValue = args[0] switch
            {
                StringValue s => NumberValueFactory.Create(s.Value.Length),
                ArrayValue a => NumberValueFactory.Create(a.Length),
                _ => throw new RuntimeException("len() 期望字符串或数组")
            };
            return lenValue;
        });

        private static readonly FunctionValue keys = new(nameof(keys), static args =>
        {
            if (args.Count != 1)
                throw new RuntimeException("keys() 期望 1 个参数");

            if (args[0] is not ObjectValue obj)
                throw new RuntimeException("keys() 期望对象");

            var keys = obj.Properties.Keys.Select(k => StringValue.Create(k)).Cast<Value>().ToList();
            return new ArrayValue(keys);
        });

        private static readonly FunctionValue @bool = new(nameof(@bool), static args =>
        {
            if (args.Count != 1)
                throw new RuntimeException("bool() 期望 1 个参数");

            return args[0] switch
            {
                NumberValue<int> n_int32 => BoolValue.Create(n_int32.Value > 0),
                StringValue s => BoolValue.Create(bool.TryParse(s.Value, out var value)),
                BoolValue b => b,
                _ => throw new RuntimeException("bool() 无法转换该值")
            };
        });

        private static readonly FunctionValue @int = new(nameof(@int), static args =>
        {
            if (args.Count != 1)
                throw new RuntimeException("int() 期望 1 个参数");

            return args[0] switch
            {
                NumberValue<int> number_int32 => number_int32,
                NumberValue<double> number_double => NumberValueFactory.Create((int)number_double.Value),
                StringValue s => NumberValueFactory.Create(int.TryParse(s.Value, out var value) ? value : 0),
                BoolValue b => NumberValueFactory.Create(b.Value ? 1 : 0),
                _ => throw new RuntimeException("int() 无法转换该值")
            };
        });

        private static readonly FunctionValue @double = new(nameof(@double), static args =>
        {
            if (args.Count != 1)
                throw new RuntimeException("double() 期望 1 个参数");

            return args[0] switch
            {
                NumberValue<int> number_int32 => NumberValueFactory.Create(number_int32.Value),
                NumberValue<double> number_double => number_double,
                StringValue s => NumberValueFactory.Create(double.Parse(s.Value)),
                BoolValue b => NumberValueFactory.Create(b.Value ? 1 : 0),
                _ => throw new RuntimeException("double() 无法转换该值")
            };
        });

        private static readonly FunctionValue str = new(nameof(str), static args =>
        {
            if (args.Count != 1)
                throw new RuntimeException("str() 期望 1 个参数");

            return args[0] switch
            {
                NumberValue<int> number_int32 => StringValue.Create(number_int32.Value.ToString()),
                NumberValue<double> number_double => StringValue.Create(number_double.Value.ToString()),
                StringValue s => StringValue.Create(s.Value),
                BoolValue b => StringValue.Create(b.Value ? Boolean.TrueString : Boolean.FalseString),
                _ => throw new RuntimeException("str() 无法转换该值")
            };
        });

        /// <summary>
        /// import(path) — 动态加载脚本模块
        ///
        /// 用法:
        ///   let mod = import("pages/home.script")
        ///   mod.someFunction()
        ///
        /// 与静态 import {} from "" 的区别:
        ///   - 静态 import 在脚本解析时加载，成员直接绑定到全局作用域
        ///   - 动态 import() 在运行时按需加载，返回整个模块的 ObjectValue
        ///   - 模块缓存：同一路径只加载一次，后续调用返回缓存的 exports
        /// </summary>
        private static readonly FunctionValue import = new("import", static async (env, args) =>
        {
            if (args.Count != 1 || args[0] is not StringValue path)
                throw new RuntimeException("import() 期望 1 个字符串路径参数");
            var exports = await env.ImportResolver.ResolveAsync(path.Value);
            return exports;
        });

        public static Dictionary<string, Value> SystemValues { get; private set; } 

        static BuiltinCache()
        {
            var values = new Dictionary<string, Value>()
            {
                { nameof(debug)   ,  debug     },
                { nameof(now)     ,  now       },
                { nameof(date)    ,  date      },
                { nameof(timespan),  timespan  },
                { nameof(@try) ,  @try   },
                { nameof(sleep)   ,  sleep     },
                { nameof(@typeof) ,  @typeof   },
                { nameof(print)   ,  print     },
                { nameof(range)   ,  range     },
                { nameof(len)     ,  len       },
                { nameof(keys)    ,  keys      },
                { nameof(@bool)   ,  @bool     },
                { nameof(@int)    ,  @int      },
                { nameof(@double) ,  @double   },
                { nameof(str)     ,  str       },
                { "import"         ,  import    },
            };
            SystemValues = values;
        }

        public static void RegisterAll(Scope scope)
        {
            foreach (var item in SystemValues)
            {
                var name = item.Key;
                var value = item.Value;
                scope.Define(name, value, isMutable: false);
            }

        }
    }
}