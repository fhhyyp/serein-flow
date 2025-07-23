using Serein.Library;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serein.Library
{


    public static class ScriptBaseFunc
    {
        public static DateTime now() => DateTime.Now;

        #region 常用的类型转换
        public static bool @bool(object value)
        {
            return ConvertHelper.ValueParse<bool>(value);
        }
        public static byte @byte(object value)
        {
            return ConvertHelper.ValueParse<byte>(value);
        }
        public static decimal @decimal(object value)
        {
            return ConvertHelper.ValueParse<decimal>(value);
        }
        public static float @float(object value)
        {
            return ConvertHelper.ValueParse<float>(value);
        }
        public static double @double(object value)
        {
            return ConvertHelper.ValueParse<double>(value);
        }
        public static int @int(object value)
        {
            return ConvertHelper.ValueParse<int>(value);
        }
        public static int @long(object value)
        {
            return ConvertHelper.ValueParse<int>(value);
        }

        #endregion

        public static int len(object target)
        {
            // 获取数组或集合对象
            // 访问数组或集合中的指定索引
            if (target is Array array)
            {
                return array.Length;
            }
            else if (target is string chars)
            {
                return chars.Length;
            }
            else if (target is IDictionary<object, object> dict)
            {
                return dict.Count;
            }
            else if (target is IList<object> list)
            {
                return list.Count;
            }
            else
            {
                throw new ArgumentException($"并非有效集合");
            }
        }


        public static string str(object obj)
        {
            return obj?.ToString() ?? string.Empty;
        }


        public static object global(string name)
        {
            return SereinEnv.GetFlowGlobalData(name);
        }

        public static Type type(object type)
        {
            return type.GetType();
        }

        public static void log(object value)
        {
            SereinEnv.WriteLine(InfoType.INFO, value?.ToString());
        }

        public static async Task sleep(object value)
        {
            if (value is int @int)
            {
                Console.WriteLine($"等待{@int}ms");
                await Task.Delay(@int);
            }
            else if (value is TimeSpan timeSpan)
            {
                Console.WriteLine($"等待{timeSpan}");
                await Task.Delay(timeSpan);
            }

        }
    }
}
