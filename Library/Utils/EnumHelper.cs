﻿using Serein.Library.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Serein.Library.Utils
{
    public static class EnumHelper
    {
        public static bool TryConvertEnum<T>(this string value, out T result) where T : struct, Enum
        {
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out T tempResult) && Enum.IsDefined(typeof(T), tempResult))
            {
                result = tempResult;
                return true;
            }
            result = default;
            return false;
        }
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
