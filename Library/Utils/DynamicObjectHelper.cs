using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    public class DynamicObjectHelper
    {
        // 类型缓存，键为类型的唯一名称（可以根据实际需求调整生成方式）
        static Dictionary<string, Type> typeCache = new Dictionary<string, Type>();

        public static object Resolve(IDictionary<string, object> properties, string typeName)
        {
            var obj = CreateObjectWithProperties(properties, typeName);
            //SetPropertyValues(obj, properties);
            return obj;
        }
        public static bool TryResolve(IDictionary<string, object> properties, string typeName, out object result)
        {
            result = CreateObjectWithProperties(properties, typeName);
            bool success = SetPropertyValuesWithValidation(result, properties);
            return success;
            // 打印赋值结果

        }
        // 递归方法：打印对象属性及类型
        public static void PrintObjectProperties(object obj, string indent = "")
        {
            var objType = obj.GetType();
            foreach (var prop in objType.GetProperties())
            {
                var value = prop.GetValue(obj);
                SereinEnv.WriteLine(InfoType.INFO, $"{indent}{prop.Name} (Type: {prop.PropertyType.Name}): {value}");

                if (value != null)
                {
                    if (prop.PropertyType.IsArray) // 处理数组类型
                    {
                        var array = (Array)value;
                        SereinEnv.WriteLine(InfoType.INFO, $"{indent}{prop.Name} is an array with {array.Length} elements:");
                        for (int i = 0; i < array.Length; i++)
                        {
                            var element = array.GetValue(i);
                            if (element != null && element.GetType().IsClass && !(element is string))
                            {
                                SereinEnv.WriteLine(InfoType.INFO, $"{indent}\tArray[{i}] (Type: {element.GetType().Name}) contains a nested object:");
                                PrintObjectProperties(element, indent + "\t\t");
                            }
                            else
                            {
                                SereinEnv.WriteLine(InfoType.INFO, $"{indent}\tArray[{i}] (Type: {element?.GetType().Name}): {element}");
                            }
                        }
                    }
                    else if (value.GetType().IsClass && !(value is string)) // 处理嵌套对象
                    {
                        SereinEnv.WriteLine(InfoType.INFO, $"{indent}{prop.Name} contains a nested object:");
                        PrintObjectProperties(value, indent + "\t");
                    }
                }
            }
        }



        // 方法 1: 创建动态类型及其对象实例
        public static object CreateObjectWithProperties(IDictionary<string, object> properties, string typeName)
        {
            // 如果类型已经缓存，直接返回缓存的类型
            if (typeCache.ContainsKey(typeName))
            {
                return Activator.CreateInstance(typeCache[typeName]);
            }

            // 定义动态程序集和模块
            var assemblyName = new AssemblyName("DynamicAssembly");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

            // 定义动态类型
            var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public);

            // 为每个属性名和值添加相应的属性到动态类型中
            foreach (var kvp in properties)
            {
                string propName = kvp.Key;
                object propValue = kvp.Value;
                Type propType;

                if (propValue is IList<Dictionary<string, object>>) // 处理数组类型
                {
                    var nestedPropValue = (propValue as IList<Dictionary<string, object>>)[0];
                    var nestedType = CreateObjectWithProperties(nestedPropValue, $"{propName}Element");
                    propType = nestedType.GetType().MakeArrayType(); // 创建数组类型
                }
                else if (propValue is Dictionary<string, object> nestedProperties)
                {
                    // 如果值是嵌套的字典，递归创建嵌套类型
                    propType = CreateObjectWithProperties(nestedProperties, $"{typeName}_{propName}").GetType();
                }
                else
                {
                    // 如果是普通类型，使用值的类型
                    propType = propValue?.GetType() ?? typeof(object);
                }

                // 定义私有字段和公共属性
                var fieldBuilder = typeBuilder.DefineField("_" + propName, propType, FieldAttributes.Private);
                var propertyBuilder = typeBuilder.DefineProperty(propName, PropertyAttributes.HasDefault, propType, null);

                // 定义 getter 方法
                var getMethodBuilder = typeBuilder.DefineMethod(
                    "get_" + propName,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    propType,
                    Type.EmptyTypes);

                var getIL = getMethodBuilder.GetILGenerator();
                getIL.Emit(OpCodes.Ldarg_0);
                getIL.Emit(OpCodes.Ldfld, fieldBuilder);
                getIL.Emit(OpCodes.Ret);

                // 定义 setter 方法
                var setMethodBuilder = typeBuilder.DefineMethod(
                    "set_" + propName,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    null,
                    new Type[] { propType });

                var setIL = setMethodBuilder.GetILGenerator();
                setIL.Emit(OpCodes.Ldarg_0);
                setIL.Emit(OpCodes.Ldarg_1);
                setIL.Emit(OpCodes.Stfld, fieldBuilder);
                setIL.Emit(OpCodes.Ret);

                // 将 getter 和 setter 方法添加到属性
                propertyBuilder.SetGetMethod(getMethodBuilder);
                propertyBuilder.SetSetMethod(setMethodBuilder);
            }

            // 创建类型并缓存
            var dynamicType = typeBuilder.CreateType();
            typeCache[typeName] = dynamicType;

            // 创建对象实例
            return Activator.CreateInstance(dynamicType);
        }

        // 方法 2: 递归设置对象的属性值
        public static void SetPropertyValues(object obj, Dictionary<string, object> properties)
        {
            var objType = obj.GetType();

            foreach (var kvp in properties)
            {
                var propInfo = objType.GetProperty(kvp.Key);
                object value = kvp.Value;

                // 如果值是嵌套的字典类型，递归处理嵌套对象
                if (value is Dictionary<string, object> nestedProperties)
                {
                    // 创建嵌套对象
                    var nestedObj = Activator.CreateInstance(propInfo.PropertyType);

                    // 递归设置嵌套对象的值
                    SetPropertyValues(nestedObj, nestedProperties);

                    // 将嵌套对象赋值给属性
                    propInfo.SetValue(obj, nestedObj);
                }
                else
                {
                    // 直接赋值给属性
                    propInfo.SetValue(obj, value);
                }
            }
        }
        // 方法 2: 递归设置对象的属性值（带验证）

        public static bool SetPropertyValuesWithValidation(object obj, IDictionary<string, object> properties)
        {
            var objType = obj.GetType();
            bool allSuccessful = true; // 标记是否所有属性赋值成功

            foreach (var kvp in properties)
            {
                var propName = kvp.Key;
                var propValue = kvp.Value;

                var propInfo = objType.GetProperty(propName);

                if (propInfo == null)
                {
                    // 属性不存在，打印警告并标记失败
                    SereinEnv.WriteLine(InfoType.WARN, $"属性 '{propName}' 不存在于类型 '{objType.Name}' 中，跳过赋值。");
                    allSuccessful = false;
                    continue;
                }

                // 检查属性类型是否与要赋的值兼容
                var targetType = propInfo.PropertyType;
                if (!IsCompatibleType(targetType, propValue))
                {
                    // 如果类型不兼容，打印错误并标记失败
                    SereinEnv.WriteLine(InfoType.ERROR, $"无法将类型 '{propValue?.GetType().Name}' 赋值给属性 '{propName}' (Type: {targetType.Name})，跳过赋值。");
                    allSuccessful = false;
                    continue;
                }

                try
                {
                    // 如果值是一个嵌套对象，递归赋值
                    if (propValue is Dictionary<string, object> nestedProperties)
                    {
                        var nestedObj = Activator.CreateInstance(propInfo.PropertyType);
                        if (nestedObj != null && SetPropertyValuesWithValidation(nestedObj, nestedProperties))
                        {
                            propInfo.SetValue(obj, nestedObj);
                        }
                        else
                        {
                            allSuccessful = false; // 嵌套赋值失败
                        }
                    }
                    else if (propValue is IList<Dictionary<string, object>> list) // 处理列表
                    {
                        // 获取目标类型的数组元素类型
                        var elementType = propInfo.PropertyType.GetElementType();
                        if (elementType != null)
                        {
                            var array = Array.CreateInstance(elementType, list.Count);

                            for (int i = 0; i < list.Count; i++)
                            {
                                var item = Activator.CreateInstance(elementType);
                                if (item != null && SetPropertyValuesWithValidation(item, list[i]))
                                {
                                    array.SetValue(item, i);
                                }
                                else
                                {
                                    allSuccessful = false; // 赋值失败
                                }
                            }
                            propInfo.SetValue(obj, array);
                        }
                    }
                    else
                    {
                        // 直接赋值
                        propInfo.SetValue(obj, propValue);
                    }
                }
                catch (Exception ex)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"为属性 '{propName}' 赋值时发生异常：{ex.Message}");
                    allSuccessful = false;
                }
            }

            return allSuccessful;
        }

        // 检查类型兼容性的方法（支持嵌套类型）
        static bool IsCompatibleType(Type targetType, object value)
        {
            if (value == null)
            {
                // 如果值为null，且目标类型是引用类型或者可空类型，则兼容
                return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
            }

            // 检查值的类型是否与目标类型相同，或是否可以转换为目标类型
            if (targetType.IsAssignableFrom(value.GetType()))
            {
                return true;
            }

            // 处理数组类型
            if (targetType.IsArray)
            {
                // 检查数组的元素类型与值的类型兼容
                var elementType = targetType.GetElementType();
                return value is IList<Dictionary<string, object>>; // 假设值是一个列表，具体处理逻辑在赋值时
            }

            // 处理嵌套类型的情况
            if (value is Dictionary<string, object> && targetType.IsClass && !targetType.IsPrimitive)
            {
                // 如果目标类型是一个复杂对象，并且值是一个字典，可能是嵌套对象
                return true; // 假设可以递归处理嵌套对象
            }

            return false;
        }


    }
}
