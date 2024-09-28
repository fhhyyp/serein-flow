using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Serein.Library.Entity
{

    /// <summary>
    /// 参数
    /// </summary>
    public class ExplicitData
    {
        /// <summary>
        /// 索引
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        /// 是否为显式参数（固定值/表达式）
        /// </summary>
        public bool IsExplicitData { get; set; }
        /// <summary>
        /// 转换器 IEnumConvertor<,>
        /// </summary>
        public Func<object, object> Convertor { get; set; }
        ///// <summary>
        ///// 显式类型
        ///// </summary>
        public Type ExplicitType { get; set; }

        ///// <summary>
        ///// 显示类型编号>
        ///// </summary>
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
        /// 入参值（在UI上输入的文本内容）
        /// </summary>

        public string DataValue { get; set; }

        public object[] Items { get; set; }

        public ExplicitData Clone() => new ExplicitData()
        {
            Index = Index,
            IsExplicitData = IsExplicitData,
            ExplicitType = ExplicitType,
            ExplicitTypeName = ExplicitTypeName,
            Convertor = Convertor,
            DataType = DataType,
            ParameterName = ParameterName,
            DataValue = string.IsNullOrEmpty(DataValue) ? string.Empty : DataValue,
            Items = Items.Select(it => it).ToArray(),
        };
    }


}
