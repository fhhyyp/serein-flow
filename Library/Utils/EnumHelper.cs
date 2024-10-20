using System;
using System.Reflection;

namespace Serein.Library.Utils
{

    /// <summary>
    /// 枚举工具类，用于枚举转换器
    /// </summary>
    public static class EnumHelper
    {
        /// <summary>
        /// 将字符串的字面量枚举值，转为对应的枚举值
        /// </summary>
        /// <typeparam name="TEnum">枚举</typeparam>
        /// <param name="value">枚举字面量</param>
        /// <param name="result">返回的枚举值</param>
        /// <returns>是否转换成功</returns>
        public static bool TryConvertEnum<TEnum>(this string value, out TEnum result) where TEnum : struct, Enum
        {
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out TEnum tempResult) && Enum.IsDefined(typeof(TEnum), tempResult))
            {
                result = tempResult;
                return true;
            }
            result = default;
            return false;
        }
        
        

        /// <summary>
        /// 从枚举值的 BindValueAttribute 特性中 获取绑定的参数（用于绑定了某些内容的枚举值）
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <typeparam name="TResult">返回类型</typeparam>
        /// <param name="enumValue">枚举值</param>
        /// <param name="valueSelector">选择什么参数</param>
        /// <returns></returns>
        public static TResult GetBoundValue<TEnum, TResult>(TEnum enumValue, Func<BindValueAttribute, object> valueSelector)
            where TEnum : Enum
        {
            var fieldInfo = typeof(TEnum).GetField(enumValue.ToString());
            var attribute = fieldInfo.GetCustomAttribute<BindValueAttribute>();

            return attribute != null ? (TResult)valueSelector(attribute) : default;
        }

        public static object GetBoundValue(Type enumType,object enumValue, Func<BindValueAttribute, object> valueSelector)
        {
            var fieldInfo = enumType.GetField(enumValue.ToString());
            var attribute = fieldInfo.GetCustomAttribute<BindValueAttribute>();

            return attribute != null ? valueSelector(attribute) : default;
        }


        /// <summary>
        /// 从枚举值从获取自定义特性的成员，并自动转换类型
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <typeparam name="TAttribute">自定义特性类型</typeparam>
        /// <typeparam name="TResult">返回类型</typeparam>
        /// <param name="enumValue">枚举值</param>
        /// <param name="valueSelector">特性成员选择</param>
        /// <returns></returns>
       public static TResult GetBoundValue<TEnum, TAttribute, TResult>(TEnum enumValue,
                                                                       Func<TAttribute, TResult> valueSelector)
           where TEnum : Enum
           where TAttribute : Attribute
       {
           var fieldInfo = typeof(TEnum).GetField(enumValue.ToString());
           var attribute = fieldInfo.GetCustomAttribute<TAttribute>();

           return attribute != null ? valueSelector(attribute) : default;
       }



    }

}
