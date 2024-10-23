using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace Serein.Library
{

    /// <summary>
    /// 节点入参参数详情
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.Parameter)]
    public partial class ParameterDetails
    {
        private readonly IFlowEnvironment env;

        /// <summary>
        /// 对应的节点
        /// </summary>
        [PropertyInfo(IsProtection = true)]
        private NodeModelBase _nodeModel;

        /// <summary>
        /// 参数索引
        /// </summary>
        [PropertyInfo] 
        private int _index;

        /// <summary>
        /// 是否为显式参数（固定值/表达式）
        /// </summary>
        [PropertyInfo(IsNotification = true)] 
        private bool _isExplicitData ;

        /// <summary>
        /// 转换器 IEnumConvertor&lt;,&gt;
        /// </summary>
        [PropertyInfo] 
        private Func<object, object> _convertor ;

        /// <summary>
        /// 显式类型
        /// </summary>
        [PropertyInfo]
        private Type _explicitType ;

        /// <summary>
        /// 目前存在三种状态：Select/Bool/Value
        /// <para>Select : 枚举值</para>
        /// <para>Bool   : 布尔类型</para>
        /// <para>Value  ： 除以上类型之外的任意参数</para>
        /// </summary>
        [PropertyInfo] 
        private string _explicitTypeName ;

        /// <summary>
        /// 方法需要的类型
        /// </summary>
        [PropertyInfo] 
        private Type _dataType ;

        /// <summary>
        /// 方法入参参数名称
        /// </summary>
        [PropertyInfo] 
        private string _name ;

        /// <summary>
        /// 自定义的方法入参数据
        /// </summary>
        [PropertyInfo(IsNotification = true)] // IsPrint = true
        private string _dataValue;

        /// <summary>
        /// 如果是引用类型，拷贝时不会发生改变。
        /// </summary>
        [PropertyInfo(IsNotification = true)] 
        private string[] _items ;
    }


    public partial class ParameterDetails
    {
        /// <summary>
        /// 为节点实例化新的入参描述
        /// </summary>
        public ParameterDetails(IFlowEnvironment env, NodeModelBase nodeModel)
        {
            this.env = env;
            this.NodeModel = nodeModel;
        }
        /// <summary>
        /// 通过参数信息加载实体，用于加载项目文件、远程连接的场景
        /// </summary>
        /// <param name="info">参数信息</param>
        public ParameterDetails(ParameterDetailsInfo info)
        {
            //this.env = env;
            Index = info.Index;
            Name = info.Name;
            DataType = Type.GetType(info.DataTypeFullName);
            ExplicitType = Type.GetType(info.ExplicitTypeFullName);
            ExplicitTypeName = info.ExplicitTypeName;
            Items = info.Items;
        }

        /// <summary>
        /// 用于创建元数据
        /// </summary>
        /// <param name="info">方法参数信息</param>
        public ParameterDetails()
        {
            
        }

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
                Name = Name,
                ExplicitTypeFullName = ExplicitType.FullName,
                ExplicitTypeName = ExplicitTypeName,
                Items = Items,
            };
        }

        /// <summary>
        /// 为某个节点拷贝方法描述的入参描述
        /// </summary>
        /// <param name="env">运行环境</param>
        /// <param name="nodeGuid">运行环境</param>
        /// <returns></returns>
        public ParameterDetails CloneOfClone(IFlowEnvironment env, NodeModelBase nodeModel)
        {
            var pd = new ParameterDetails(env, nodeModel)
            {
                Index = this.Index,
                IsExplicitData = this.IsExplicitData,
                ExplicitType = this.ExplicitType,
                ExplicitTypeName = this.ExplicitTypeName,
                Convertor = this.Convertor,
                DataType = this.DataType,
                Name = this.Name,
                DataValue = string.IsNullOrEmpty(DataValue) ? string.Empty : DataValue,
                Items = this.Items?.Select(it => it).ToArray(),
            };
            return pd;
        }
    }




    ///// <summary>
    ///// 节点入参参数详情
    ///// </summary>

    //public partial class TempParameterDetails
    //{
    //    private readonly MethodDetails methodDetails;

    //    /// <summary>
    //    /// 参数索引
    //    /// </summary>
    //    public int Index { get; set; }
    //    /// <summary>
    //    /// 是否为显式参数（固定值/表达式）
    //    /// </summary>
    //    public bool IsExplicitData { get; set; }
    //    /// <summary>
    //    /// 转换器 IEnumConvertor&lt;,&gt;
    //    /// </summary>
    //    public Func<object, object> Convertor { get; set; }
    //    /// <summary>
    //    /// 显式类型
    //    /// </summary>
    //    public Type ExplicitType { get; set; }

    //    /// <summary>
    //    /// 目前存在三种状态：Select/Bool/Value
    //    /// <para>Select : 枚举值</para>
    //    /// <para>Bool   : 布尔类型</para>
    //    /// <para>Value  ： 除以上类型之外的任意参数</para>
    //    /// </summary>
    //    public string ExplicitTypeName { get; set; }

    //    /// <summary>
    //    /// 方法需要的类型
    //    /// </summary>
    //    public Type DataType { get; set; }

    //    /// <summary>
    //    /// 方法入参参数名称
    //    /// </summary>
    //    public string Name { get; set; }


    //    private string _dataValue;
    //    /// <summary>
    //    /// 入参值（在UI上输入的文本内容）
    //    /// </summary>

    //    public string DataValue
    //    {
    //        get => _dataValue; set
    //        {
    //            _dataValue = value;
    //            Console.WriteLine($"更改了{value}");
    //        }
    //    }

    //    /// <summary>
    //    /// 如果是引用类型，拷贝时不会发生改变。
    //    /// </summary>
    //    public string[] Items { get; set; }
    //}

}
