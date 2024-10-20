using Serein.Library;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Net462DllTest.Utils
{
    public interface IGSModel<TKey, TModel>
    {
        //TModel Value { get; set; }
        void Set(TKey tEnum, object value);
        object Get(TKey tEnum);

    }

  
    /// <summary>
    /// 通过 Emit 创建 set/get 委托
    /// </summary>
    public class GSModel<TKey, TModel> : IGSModel<TKey, TModel>
        where TKey : struct, Enum
        where TModel : class
    {
        private TModel Value;

        public GSModel(TModel Model)
        {
            this.Value = Model;
        }
        // 缓存创建好的setter和getter委托
        private readonly Dictionary<TKey, Action<TModel, object>> _setterCache = new Dictionary<TKey, Action<TModel, object>>();
        private readonly Dictionary<TKey, Func<TModel, object>> _getterCache = new Dictionary<TKey, Func<TModel, object>>();

        public void Set(TKey tEnum, object value)
        {
            if (!_setterCache.TryGetValue(tEnum, out var setter))
            {
                PropertyInfo property = GetPropertyByEnum(tEnum);
                if (property == null)
                {
                    _setterCache[tEnum] = (s, o) => throw new ArgumentException($"没有对应的Model属性{{{tEnum}");
                }
                else
                {
                    // 创建并缓存setter委托
                    setter = CreateSetter(property);
                    _setterCache[tEnum] = setter;
                }
            }

            // 使用缓存的setter委托设置值
            setter(Value, value);
        }

        public object Get(TKey tEnum)
        {
            if (!_getterCache.TryGetValue(tEnum, out var getter))
            {
                PropertyInfo property = GetPropertyByEnum(tEnum);
                if (property == null)
                {
                    _setterCache[tEnum] = (s, o) => throw new ArgumentException($"没有对应的Model属性{tEnum}");
                }
                else
                {
                    // 创建并缓存getter委托
                    getter = CreateGetter(property);
                    _getterCache[tEnum] = getter;
                }

            }

            // 使用缓存的getter委托获取值
            return getter(Value);
        }

        private PropertyInfo GetPropertyByEnum(TKey tEnum)
        {
            foreach (var property in typeof(TModel).GetProperties())
            {
                var attribute = property.GetCustomAttribute<BindValueAttribute>();
                if (attribute?.Value?.GetType()?.IsEnum == true)
                {
                    if (attribute.Value is TKey @enum && @enum.Equals(tEnum))
                    {
                        return property;
                    }
                }

            }
            return null;
        }






        // 动态创建调用Setter方法
        private Action<TModel, object> CreateSetter(PropertyInfo property)
        {
            var method = new DynamicMethod("Set" + property.Name, null, new[] { typeof(TModel), typeof(object) }, true);
            var il = method.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0); // 加载实例（PlcVarValue）
            il.Emit(OpCodes.Ldarg_1); // 加载值（object）

            if (property.PropertyType.IsValueType)
            {
                il.Emit(OpCodes.Unbox_Any, property.PropertyType); // 解箱并转换为值类型
            }
            else
            {
                il.Emit(OpCodes.Castclass, property.PropertyType); // 引用类型转换
            }

            il.Emit(OpCodes.Callvirt, property.GetSetMethod()); // 调用属性的Setter方法
            il.Emit(OpCodes.Ret); // 返回



            return (Action<TModel, object>)method.CreateDelegate(typeof(Action<TModel, object>));
        }

        /// <summary>
        /// 动态创建调用Getter方法
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private Func<TModel, object> CreateGetter(PropertyInfo property)
        {
            var method = new DynamicMethod("Get" + property.Name, typeof(object), new[] { typeof(TModel) }, true);
            var il = method.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0); // 加载实例（PlcVarValue）
            il.Emit(OpCodes.Callvirt, property.GetGetMethod()); // 调用属性的Getter方法

            if (property.PropertyType.IsValueType)
            {
                il.Emit(OpCodes.Box, property.PropertyType); // 值类型需要装箱
            }

            il.Emit(OpCodes.Ret); // 返回

            return (Func<TModel, object>)method.CreateDelegate(typeof(Func<TModel, object>));
        }








    }
}
