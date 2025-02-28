using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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

        ///// <summary>
        ///// 转换器 IEnumConvertor&lt;,&gt;
        ///// </summary>
        //[PropertyInfo] 
        //private Func<object, object> _convertor ;

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
                //Convertor = this.Convertor,
                DataType = this.DataType,
                Name = this.Name,
                DataValue = string.IsNullOrEmpty(DataValue) ? string.Empty : DataValue,
                Items = this.Items?.Select(it => it).ToArray(),
                IsParams = this.IsParams,

            };
            return pd;
        }

        /// <summary>
        /// 转为方法入参数据
        /// </summary>
        /// <returns></returns>
        public async ValueTask<object> ToMethodArgData(IDynamicContext context)
        {
            var nodeModel = NodeModel;
            var env = nodeModel.Env;
            #region 显然的流程基本类型
            // 返回运行环境
            if (typeof(IFlowEnvironment).IsAssignableFrom(DataType))
            {
                return env;
            }
            // 返回流程上下文
            if (typeof(IDynamicContext).IsAssignableFrom(DataType))
            {
                return context;
            }
            // 显式设置的参数
            if (IsExplicitData && !DataValue.StartsWith("@", StringComparison.OrdinalIgnoreCase))
            {
                return DataValue.ToConvert(DataType); // 并非表达式，同时是显式设置的参数
            } 
            #endregion
            #region “枚举-类型”转换器
            if (ExplicitType.IsEnum && DataType != ExplicitType)
            {
                var resultEnum = Enum.Parse(ExplicitType, DataValue);
                // 获取绑定的类型
                var type = EnumHelper.GetBoundValue(ExplicitType, resultEnum, attr => attr.Value);
                if (type is Type enumBindType && !(enumBindType is null))
                {
                    var value = nodeModel.Env.IOC.Instantiate(enumBindType);
                    return value;
                }
            } 
            #endregion

            // 需要获取预入参数据
            object inputParameter;
            #region （默认的）从运行时上游节点获取其返回值
            if (ArgDataSourceType == ConnectionArgSourceType.GetPreviousNodeData)
            {
                var previousNode = context.GetPreviousNode(nodeModel);
                if (previousNode is null)
                {
                    inputParameter = null;
                }
                else
                {
                    inputParameter = context.GetFlowData(previousNode.Guid); // 当前传递的数据
                }
            }
            #endregion
            #region  从指定节点获取其返回值
            else if (ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeData)
            {
                // 获取指定节点的数据
                // 如果指定节点没有被执行，会返回null
                // 如果执行过，会获取上一次执行结果作为预入参数据
                inputParameter = context.GetFlowData(ArgDataSourceNodeGuid);
            }
            #endregion
            #region 立刻执行指定节点，然后获取返回值
            else if (ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeDataOfInvoke)
            {
                // 立刻调用对应节点获取数据。
                try
                {
                    var result = await env.InvokeNodeAsync(context, ArgDataSourceNodeGuid);
                    inputParameter = result;
                }
                catch (Exception ex)
                {
                    context.NextOrientation = ConnectionInvokeType.IsError;
                    context.ExceptionOfRuning = ex;
                    throw;
                }
            }
            #endregion
            #region 意料之外的参数
            else
            {
                throw new Exception("节点执行方法获取入参参数时，ConnectionArgSourceType枚举是意外的枚举值");
            }
            #endregion
            #region 判断是否执行表达式
            if (IsExplicitData)
            {
                // @Get 表达式 （从上一节点获取对象）
                if (DataValue.StartsWith("@get", StringComparison.OrdinalIgnoreCase))
                {
                    inputParameter = SerinExpressionEvaluator.Evaluate(DataValue, inputParameter, out _);
                }

                // @DTC 表达式 （Data type conversion）
                else if (DataValue.StartsWith("@dtc", StringComparison.OrdinalIgnoreCase))
                {
                    inputParameter = SerinExpressionEvaluator.Evaluate(DataValue, inputParameter, out _);
                }

                // @Data 表达式 （获取全局数据）
                else if (DataValue.StartsWith("@data", StringComparison.OrdinalIgnoreCase))
                {
                    inputParameter = SerinExpressionEvaluator.Evaluate(DataValue, inputParameter, out _);
                }

            }

            #endregion

            // 对引用类型检查 null
            if (!DataType.IsValueType && inputParameter is null)
            {
                throw new Exception($"[arg{Index}][{Name}][{DataType}]参数不能为null");
            }
            if (DataType == typeof(string)) // 转为字符串
            {
                return inputParameter.ToString();
            }
            var inputParameterType = inputParameter.GetType();
            if (DataType.IsSubclassOf(inputParameterType)) // 入参类型 是 预入参数据类型 的 子类/实现类 
            {
                // 方法入参中，父类不能隐式转为子类，这里需要进行强制转换
                return ObjectConvertHelper.ConvertParentToChild(inputParameter, DataType);
            }
            if (DataType.IsAssignableFrom(inputParameterType))  // 入参类型 是 预入参数据类型 的 父类/接口
            {
                return inputParameter;
            }

            throw new Exception($"[arg{Index}][{Name}][{DataType}]入参类型不符合，当前预入参类型为{inputParameterType}");
        }

        public override string ToString()
        {
            return $"[{this.Index}] {this.Name} : {this.DataType?.FullName}";
        }
    }





}
