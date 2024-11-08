using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Serein.Library
{

    /// <summary>
    /// 节点入参参数详情
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.Parameter)]
    public partial class ParameterDetails
    {
        // private readonly IFlowEnvironment env;

        /// <summary>
        /// 所在的节点
        /// </summary>
        [PropertyInfo(IsProtection = true)]
        private NodeModelBase _nodeModel;

        /// <summary>
        /// 参数索引
        /// </summary>
        [PropertyInfo] 
        private int _index;

        /// <summary>
        /// <para>是否为显式参数（固定值/表达式）</para>
        /// <para>如果为 true ，则使用UI输入的文本值作为入参数据（过程中会尽可能转为类型需要的数据）。</para>
        /// <para>如果为 false ，则根据 ArgDataSourceType 调用相应节点的GetFlowData()方法，获取返回的数据作为入参数据。</para>
        /// </summary>
        [PropertyInfo(IsNotification = true)] 
        private bool _isExplicitData ;

        /// <summary>
        /// 转换器 IEnumConvertor&lt;,&gt;
        /// </summary>
        [PropertyInfo] 
        private Func<object, object> _convertor ;

        /// <summary>
        /// 方法入参若无相关转换器特性标注，则无需关注该变量。该变量用于需要用到枚举BinValue转换器时，指示相应的入参变量需要转为的类型。
        /// </summary>
        [PropertyInfo]
        private Type _explicitType ;

        /// <summary>
        /// 目前存在三种状态：Select/Bool/Value
        /// <para>Select : 枚举值/可选值</para>
        /// <para>Bool   : 布尔类型</para>
        /// <para>Value  ：除以上类型之外的任意参数</para>
        /// </summary>
        [PropertyInfo] 
        private string _explicitTypeName ;

        /// <summary>
        /// 入参数据来源。默认使用上一节点作为入参数据。
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private ConnectionArgSourceType _argDataSourceType = ConnectionArgSourceType.GetPreviousNodeData;

        /// <summary>
        /// 当 ArgDataSourceType 不为 GetPreviousNodeData 时（从运行时上一节点获取数据）。
        /// 则通过当前上下文，获取该Guid对应的数据作为预处理的入参参数。
        /// </summary>
        [PropertyInfo]
        private string _argDataSourceNodeGuid;

        /// <summary>
        /// 方法入参需要的类型。
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
        /// 只有当ExplicitTypeName 为 Select 时，才会需要该成员。
        /// </summary>
        [PropertyInfo(IsNotification = true)] 
        private string[] _items ;

        /// <summary>
        /// 指示该属性是可变参数的其中一员（可变参数为数组类型）
        /// </summary>
        [PropertyInfo]
        private bool _isParams;
    }


    public partial class ParameterDetails
    {

        /// <summary>
        /// 用于创建元数据
        /// </summary>
        public ParameterDetails()
        {

        }
 

        /// <summary>
        /// 为节点实例化新的入参描述
        /// </summary>
        public ParameterDetails(NodeModelBase nodeModel)
        {
            this.NodeModel = nodeModel;
        }

        /// <summary>
        /// 通过参数信息加载实体，用于加载项目文件、远程连接的场景
        /// </summary>
        /// <param name="info">参数信息</param>
        public ParameterDetails(ParameterDetailsInfo info)
        {
            Index = info.Index;
            Name = info.Name;
            DataType = Type.GetType(info.DataTypeFullName);
            ExplicitType = Type.GetType(info.ExplicitTypeFullName);
            ExplicitTypeName = info.ExplicitTypeName;
            Items = info.Items;
            IsParams = info.IsParams;
        }

        /// <summary>
        /// 转为描述
        /// </summary>
        /// <returns></returns>
        public ParameterDetailsInfo ToInfo()
        {
            return new ParameterDetailsInfo
            {
                Index = this.Index,
                IsParams = this.IsParams,
                DataTypeFullName = this.DataType.FullName,
                Name = this.Name,
                ExplicitTypeFullName = this.ExplicitType.FullName,
                ExplicitTypeName = this.ExplicitTypeName,
                Items = this.Items.Select(it => it).ToArray(),
            };
        }

        /// <summary>
        /// 为某个节点从元数据中拷贝方法描述的入参描述
        /// </summary>
        /// <param name="nodeModel">对应的节点</param>
        /// <returns></returns>
        public ParameterDetails CloneOfModel(NodeModelBase nodeModel)
        {
            var pd = new ParameterDetails(nodeModel)
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
                IsParams = this.IsParams,

            };
            return pd;
        }

        public override string ToString()
        {
            if(_convertor is null)
            {
                return $"[{this.Index}] {this.Name} : {this.DataType?.FullName}";
            }
            else
            {

            }
            return $"[{this.Index}] {this.Name} : {this.ExplicitType.FullName} -> {this.DataType.FullName}";
        }
    }





}
