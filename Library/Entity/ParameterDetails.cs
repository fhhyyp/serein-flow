using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Serein.Library.Entity
{

    /// <summary>
    /// 方法入参描述
    /// </summary>
    public class ParameterDetailsInfo
    {
        /// <summary>
        /// 参数索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 方法需要的类型
        /// </summary>
        public string DataTypeFullName { get; set; }

        /// <summary>
        /// 方法入参参数名称
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 节点入参参数详情
    /// </summary>
    public class ParameterDetails
    {
        /// <summary>
        /// 转为描述
        /// </summary>
        /// <returns></returns>
        public ParameterDetailsInfo ToInfo()
        {
            return new ParameterDetailsInfo
            {
                Index = Index,
                DataTypeFullName = DataType.FullName,
                Name = Name
            };
        }

        /// <summary>
        /// 拷贝新的对象。
        /// </summary>
        /// <returns></returns>
        public ParameterDetails Clone() => new ParameterDetails()
        {
            Index = Index,
            IsExplicitData = IsExplicitData,
            ExplicitType = ExplicitType,
            ExplicitTypeName = ExplicitTypeName,
            Convertor = Convertor,
            DataType = DataType,
            Name = Name,
            DataValue = string.IsNullOrEmpty(DataValue) ? string.Empty : DataValue,
            Items = Items.Select(it => it).ToArray(),
        };

        /// <summary>
        /// 参数索引
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        /// 是否为显式参数（固定值/表达式）
        /// </summary>
        public bool IsExplicitData { get; set; }
        /// <summary>
        /// 转换器 IEnumConvertor&lt;,&gt;
        /// </summary>
        public Func<object, object> Convertor { get; set; }
        /// <summary>
        /// 显式类型
        /// </summary>
        public Type ExplicitType { get; set; }

        /// <summary>
        /// 目前存在三种状态：Select/Bool/Value
        /// <para>Select : 枚举值</para>
        /// <para>Bool   : 布尔类型</para>
        /// <para>Value  ： 除以上类型之外的任意参数</para>
        /// </summary>
        public string ExplicitTypeName { get; set; }

        /// <summary>
        /// 方法需要的类型
        /// </summary>
        public Type DataType { get; set; }

        /// <summary>
        /// 方法入参参数名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 入参值（在UI上输入的文本内容）
        /// </summary>

        public string DataValue { get; set; }

        /// <summary>
        /// 如果是引用类型，拷贝时不会发生改变。
        /// </summary>
        public object[] Items { get; set; }


    }


}
