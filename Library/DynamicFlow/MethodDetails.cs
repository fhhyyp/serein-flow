using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Dm.net.buffer.ByteArrayBuffer;

namespace Serein.DynamicFlow
{
    /// <summary>
    /// 显式参数
    /// </summary>
    public class ExplicitData
    {
        /// <summary>
        /// 索引
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        /// 是否为显式参数
        /// </summary>
        public bool IsExplicitData { get; set; }
        /// <summary>
        /// 显式类型
        /// </summary>
        public Type? ExplicitType { get; set; }

        /// <summary>
        /// 显示类型编号>
        /// </summary>
        public string ExplicitTypeName { get; set; }

        /// <summary>
        /// 方法需要的类型
        /// </summary>
        public Type DataType { get; set; }
        /// <summary>
        /// 方法入参参数名称
        /// </summary>
        public string ParameterName { get; set; }
        /// <summary>
        /// 入参值
        /// </summary>
        public string DataValue { get; set; }

        public string[] Items { get; set; }



        public ExplicitData Clone() => new()
        {
            Index = Index,
            IsExplicitData = IsExplicitData,
            ExplicitType = ExplicitType,
            DataType = DataType,
            ParameterName = ParameterName,
            ExplicitTypeName = ExplicitTypeName,
            DataValue = string.IsNullOrEmpty(DataValue) ? string.Empty : DataValue,
            Items = [.. Items],
        };
    }



    public class MethodDetails
    {
        public MethodDetails CpoyNew()
        {
            return new MethodDetails
            {
                ActingInstance = ActingInstance,
                ActingInstanceType = ActingInstanceType,
                MethodDelegate = MethodDelegate,
                MethodDynamicType = MethodDynamicType,
                MethodGuid = Guid.NewGuid().ToString(),
                MethodTips = MethodTips + " Cpoy",
                //ParameterTypes = ParameterTypes,
                ReturnType = ReturnType,
                MethodName = MethodName,
                MethodLockName = MethodLockName,
                //ExplicitDataValues = ExplicitDataValues.Select(it => "").ToArray(),
                ExplicitDatas = ExplicitDatas.Select(it => it.Clone()).ToArray(),
                //IsExplicits = IsExplicits,
            };
        }

        /// <summary>
        /// 作用实例
        /// </summary>
        public Type ActingInstanceType { get; set; }
        /// <summary>
        /// 作用实例
        /// </summary>
        public object ActingInstance { get; set; }
        /// <summary>
        /// 方法GUID
        /// </summary>
        public string MethodGuid { get; set; }
        /// <summary>
        /// 方法名称
        /// </summary>
        public string MethodName { get; set; }
        /// <summary>
        /// 方法委托
        /// </summary>
        public Delegate MethodDelegate { get; set; }
        /// <summary>
        /// 节点类型
        /// </summary>
        public DynamicNodeType MethodDynamicType { get; set; }
        /// <summary>
        /// 锁名称
        /// </summary>
        public string MethodLockName { get; set; }

        /// <summary>
        /// 方法说明
        /// </summary>
        public string MethodTips { get; set; }

        /// <summary>
        /// 参数内容
        /// </summary>
        public ExplicitData[] ExplicitDatas { get; set; }


        /// <summary>
        /// 出参类型
        /// </summary>
        public Type ReturnType { get; set; }





        public bool IsCanConnect(Type returnType)
        {
            if (ExplicitDatas.Length == 0)
            {
                // 目标不需要传参，可以舍弃结果？
                return true;
            }
            var types = ExplicitDatas.Select(it => it.DataType).ToArray();
            // 检查返回类型是否是元组类型
            if (returnType.IsGenericType && IsValueTuple(returnType))
            {
               
                return CompareGenericArguments(returnType, types);
            }
            else
            {
                int index = 0;
                if (types[index] == typeof(DynamicContext))
                {
                    index++;
                    if (types.Length == 1)
                    {
                        return true;
                    }
                }
                // 被连接节点检查自己需要的参数类型，与发起连接的节点比较返回值类型
                if (returnType == types[index])
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查元组类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool IsValueTuple(Type type)
        {
            if (!type.IsGenericType) return false;

            var genericTypeDef = type.GetGenericTypeDefinition();
            return genericTypeDef == typeof(ValueTuple<>) ||
                   genericTypeDef == typeof(ValueTuple<,>) ||
                   genericTypeDef == typeof(ValueTuple<,,>) ||
                   genericTypeDef == typeof(ValueTuple<,,,>) ||
                   genericTypeDef == typeof(ValueTuple<,,,,>) ||
                   genericTypeDef == typeof(ValueTuple<,,,,,>) ||
                   genericTypeDef == typeof(ValueTuple<,,,,,,>) ||
                   genericTypeDef == typeof(ValueTuple<,,,,,,,>);
        }

        private bool CompareGenericArguments(Type returnType, Type[] parameterTypes)
        {
            var genericArguments = returnType.GetGenericArguments();
            var length = parameterTypes.Length;

            for (int i = 0; i < genericArguments.Length; i++)
            {
                if (i >= length) return false;

                if (IsValueTuple(genericArguments[i]))
                {
                    // 如果当前参数也是 ValueTuple，递归检查嵌套的泛型参数
                    if (!CompareGenericArguments(genericArguments[i], parameterTypes.Skip(i).ToArray()))
                    {
                        return false;
                    }
                }
                else if (genericArguments[i] != parameterTypes[i])
                {
                    return false;
                }
            }

            return true;
        }
    }


}
