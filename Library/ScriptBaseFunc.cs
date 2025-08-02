using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serein.Library
{


    /// <summary>
    /// 脚本代码中常用的函数
    /// </summary>
    public static class ScriptBaseFunc
    {
        /// <summary>
        /// 获取当前时间
        /// </summary>
        /// <returns></returns>
        public static DateTime now() => DateTime.Now;

        #region 常用的类型转换
        /// <summary>
        /// 将值转换为bool类型
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool @bool(object value)
        {
            return ObjectConvertHelper.ValueParse<bool>(value);
        }

        /// <summary>
        /// 将值转换为字节类型
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte @byte(object value)
        {
            return ObjectConvertHelper.ValueParse<byte>(value);
        }

        /// <summary>
        /// 将值转换为短整型
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static decimal @decimal(object value)
        {
            return ObjectConvertHelper.ValueParse<decimal>(value);
        }

        /// <summary>
        /// 将值转换为浮点型
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static float @float(object value)
        {
            return ObjectConvertHelper.ValueParse<float>(value);
        }

        /// <summary>
        /// 将值转换为双精度浮点型
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static double @double(object value)
        {
            return ObjectConvertHelper.ValueParse<double>(value);
        }

        /// <summary>
        /// 将值转换为整数类型
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int @int(object value)
        {
            return ObjectConvertHelper.ValueParse<int>(value);
        }

        /// <summary>
        /// 将值转换为长整型
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int @long(object value)
        {
            return ObjectConvertHelper.ValueParse<int>(value);
        }

        #endregion

        /// <summary>
        /// 获取集合或数组的长度
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
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

        /// <summary>
        /// 将对象转换为字符串
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string str(object obj)
        {
            return obj?.ToString() ?? string.Empty;
        }


        #region JSON挂载方法

        /// <summary>
        /// 转为JSON对象
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static IJsonToken jsonObj(string content)
        {
            /*if (string.IsNullOrWhiteSpace(content))
            {
                return JsonHelper.Object(dict => { }) ;
            }*/
            return JsonHelper.Parse(content);
        }

        /// <summary>
        /// 转为JSON字符串
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static string jsonStr(object data)
        {
            if (data is null)
            {
                return "{}";
            }
            return JsonHelper.Serialize(data);
        }


        #endregion


        /// <summary>
        /// 获取全局数据
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static object global(string name)
        {
            return SereinEnv.GetFlowGlobalData(name);
        }

        /// <summary>
        /// 获取对象的类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type type(object type)
        {
            return type.GetType();
        }

        /// <summary>
        /// 输出内容
        /// </summary>
        /// <param name="value"></param>
        public static void log(object value)
        {
            SereinEnv.WriteLine(InfoType.INFO, value?.ToString());
        }

        /// <summary>
        /// 等待一段时间
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static async Task sleep(int value)
        {
            await Task.Delay(value);

        }
    }
}
