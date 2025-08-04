using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
namespace Serein.Library
{



    /// <summary>
    /// 节点入参参数详情
    /// </summary>
    [FlowDataProperty(ValuePath = NodeValuePath.Parameter)]
    public partial class ParameterDetails
    {
        // private readonly IFlowEnvironment env;

        /// <summary>
        /// 所在的节点
        /// </summary>
        [DataInfo(IsProtection = true)]
        private IFlowNode _nodeModel;

        /// <summary>
        /// 参数索引
        /// </summary>
        [DataInfo] 
        private int _index;

        /// <summary>
        /// <para>是否为显式参数（固定值/表达式）</para>
        /// <para>如果为 true ，则使用输入的文本值作为入参数据。</para>
        /// <para>如果为 false ，则在当前流程上下文中，根据 ArgDataSourceNodeGuid 查找到对应节点，并根据 ArgDataSourceNodeGuid 判断如何获取其返回的数据，以此作为入参数据。</para>
        /// </summary>
        [DataInfo(IsNotification = true, IsVerify =  true)] 
        private bool _isExplicitData ;

        ///// <summary>
        ///// 转换器 IEnumConvertor&lt;,&gt;
        ///// </summary>
        //[PropertyInfo] 
        //private Func<object, object> _convertor ;

        /// <summary>
        /// 方法入参若无相关转换器特性标注，则无需关注该变量。该变量用于需要用到枚举BinValue转换器时，指示相应的入参变量需要转为的类型。
        /// </summary>
        [Obsolete("转换器特性将在下一个大版本中移除")]
        [DataInfo]
        private Type _explicitType ;

        /// <summary>
        /// 目前存在三种状态：Select/Bool/Value
        /// <para>Select : 枚举值/可选值</para>
        /// <para>Bool   : 布尔类型</para>
        /// <para>Value  ：除以上类型之外的任意参数</para>
        /// </summary>
        [DataInfo] 
        private ParameterValueInputType _inputType ;

        /// <summary>
        /// 入参数据来源。默认使用上一节点作为入参数据。
        /// </summary>
        [DataInfo(IsNotification = true)]
        private ConnectionArgSourceType _argDataSourceType = ConnectionArgSourceType.GetPreviousNodeData;

        /// <summary>
        /// 当 ArgDataSourceType 不为 GetPreviousNodeData 时（从运行时上一节点获取数据）。
        /// 则通过当前上下文，获取该Guid对应的数据作为预处理的入参参数。
        /// </summary>
        [DataInfo]
        private string _argDataSourceNodeGuid;

        /// <summary>
        /// 方法入参需要的类型。
        /// </summary>
        [DataInfo] 
        private Type _dataType ;

        /// <summary>
        /// 方法入参参数名称
        /// </summary>
        [DataInfo(IsNotification = true)] 
        private string _name ;

        /// <summary>
        /// 入参注释
        /// </summary>
        [DataInfo]
        private string _description;

        /// <summary>
        /// 自定义的方法入参数据
        /// </summary>
        [DataInfo(IsNotification = true)] // IsPrint = true
        private string _dataValue;

        /// <summary>
        /// 只有当 InputType 为 Select 时，才会需要该成员。
        /// </summary>
        [DataInfo(IsNotification = true)] 
        private string[] _items ;

        /// <summary>
        /// 指示该属性是可变参数的其中一员（可变参数为数组类型）
        /// </summary>
        [DataInfo]
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
        public ParameterDetails(IFlowNode nodeModel)
        {
            this.NodeModel = nodeModel;
        }

        /// <summary>
        /// 通过参数数据加载实体，用于加载项目文件、远程连接的场景
        /// </summary>
        /// <param name="pdInfo"></param>
        /// <param name="argIndex"></param>
        public ParameterDetails(ParameterData pdInfo, int argIndex)
        {
            this.Index = argIndex;
            this.DataType = typeof(object);
            this.ArgDataSourceNodeGuid = pdInfo.SourceNodeGuid;
            this.ArgDataSourceType = EnumHelper.ConvertEnum<ConnectionArgSourceType>(pdInfo.SourceType);
            this.DataValue = pdInfo.Value;
            this.InputType = ParameterValueInputType.Input;
            this.IsExplicitData = pdInfo.State;
            this.Name = pdInfo.ArgName;
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
            InputType = info.InputType.ConvertEnum<ParameterValueInputType>();
            Items = info.Items;
            IsParams = info.IsParams;        
        }

        /// <summary>
        /// 禁止将 IFlowContext 类型显式入参设置为 true
        /// </summary>
        /// <param name="__isAllow"></param>
        /// <param name="newValue"></param>
        partial void BeforeTheIsExplicitData(ref bool __isAllow, bool newValue)
        {
            if(DataType == typeof(IFlowContext) && newValue == true)
            {
                __isAllow = false;
            }
        }

        /// <summary>
        /// 脚本节点的类型缓存。
        /// </summary>
        private Type? cacheType;
        private bool cacheIsExplicit;

        /// <summary>
        /// 脚本节点的名称变更为流程上下文时，调整 DataType 和 IsExplicitData 的值。
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        partial void OnNameChanged(string oldValue, string newValue)
        {
            if (NodeModel is null) 
                return;
            if (NodeModel.ControlType == NodeControlType.Script)
            {
                var isIgnore = StringComparison.OrdinalIgnoreCase;
                if ("context".Equals(newValue, isIgnore) ||
                    "flowcontext".Equals(newValue, isIgnore) ||
                    "flow_context".Equals(newValue, isIgnore))
                {
                    cacheType = DataType;
                    cacheIsExplicit = IsExplicitData;
                    DataType = typeof(IFlowContext);
                    IsExplicitData = false;
                }
                else 
                {
                    if (cacheType is not null)
                    {
                        DataType = cacheType;
                        IsExplicitData = cacheIsExplicit;
                    }
                }
            }
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
                ExplicitTypeFullName = this.ExplicitType?.FullName,
                InputType = this.InputType.ToString(),
                Items = this.Items?.Select(it => it).ToArray(),
            };
        }

        /// <summary>
        /// 为某个节点从元数据中拷贝方法描述的入参描述
        /// </summary>
        /// <param name="nodeModel">对应的节点</param>
        /// <returns></returns>
        public ParameterDetails CloneOfModel(IFlowNode nodeModel)
        {
            var pd = new ParameterDetails(nodeModel)
            {
                Index = this.Index,
                IsExplicitData = this.IsExplicitData,
                ExplicitType = this.ExplicitType,
                InputType = this.InputType,
                //Convertor = this.Convertor,
                DataType = this.DataType,
                Name = this.Name,
                DataValue = this.DataValue,
                Items = this.Items?.Select(it => it).ToArray(),
                IsParams = this.IsParams,
                Description = this.Description,
            };
            return pd;
        }

        /// <summary>
        /// 转为方法入参数据
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<object> ToMethodArgData(IFlowContext context)
        {
            // 1. 从缓存获取
            if (context.TryGetParamsTempData(NodeModel.Guid, Index, out var data))
                return data;

            // 2. 特定快捷类型
            if (typeof(IFlowContext).IsAssignableFrom(DataType)) return context;
            //if (typeof(IFlowEnvironment).IsAssignableFrom(DataType)) return NodeModel.Env;
            //if (typeof(IFlowNode).IsAssignableFrom(DataType)) return NodeModel;

            // 3. 显式常量参数
            if (IsExplicitData && !DataValue.StartsWith("@", StringComparison.OrdinalIgnoreCase))
                return DataValue.ToConvertValueType(DataType);

            // 4. 来自其他节点
            object inputParameter = null;
            var env = NodeModel.Env;

            if (ArgDataSourceType == ConnectionArgSourceType.GetPreviousNodeData)
            {
                var prevNodeGuid = context.GetPreviousNode(NodeModel.Guid);
                if(prevNodeGuid is null)
                {
                    inputParameter = null;
                }
                else
                {
                    var prevNodeData = context.GetFlowData(prevNodeGuid);
                    inputParameter = prevNodeData.Value;
                }
            }
            else
            {

                var prevNodeGuid = context.GetPreviousNode(NodeModel.Guid);

                if (!env.TryGetNodeModel(ArgDataSourceNodeGuid, out var sourceNode))
                    throw new Exception($"[arg{Index}] 节点[{ArgDataSourceNodeGuid}]不存在");

                if (sourceNode.IsPublic // 如果运行上一节点是[FlowCall]节点（则从该节点获取入参）
                    && env.TryGetNodeModel(prevNodeGuid, out var prevNode) 
                    && prevNode.ControlType == NodeControlType.FlowCall
                    && env.TryGetNodeModel(context.GetPreviousNode(NodeModel.Guid), out var sourceNodeTemp)
                    )
                    sourceNode = sourceNodeTemp;

                if (ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeData)
                {

                    inputParameter = context.GetFlowData(sourceNode.Guid)?.Value;
                }
                else if (ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeDataOfInvoke) // 立刻执行目标节点获取参数
                {
                    if (context.IsRecordInvokeInfo)
                    {
                        var invokeInfo = context.NewInvokeInfo(NodeModel, sourceNode, FlowInvokeInfo.InvokeType.ArgSource);
                        var result = await sourceNode.ExecutingAsync(context, CancellationToken.None);
                        inputParameter = result.Value;
                        invokeInfo.UploadResultValue(result.Value);
                        invokeInfo.UploadState(FlowInvokeInfo.RunState.Succeed);
                    }
                    else
                    {
                        var result = await sourceNode.ExecutingAsync(context, CancellationToken.None);
                        inputParameter = result.Value;
                    }
                }
                else
                {

                    throw new Exception("无效的 ArgDataSourceType");
                }
            }


            // 5. 类型转换
            if (!DataType.IsValueType && inputParameter is null)
                throw new Exception($"[arg{Index}] 参数不能为null");

            if (DataType == typeof(string))
                return inputParameter.ToString();

            var actualType = inputParameter.GetType();
            if (DataType.IsAssignableFrom(actualType))
                return inputParameter;

            if (DataType.IsSubclassOf(actualType))
                return ObjectConvertHelper.ConvertParentToChild(inputParameter, DataType);

            throw new Exception($"[arg{Index}] 类型不匹配：目标类型为 {DataType}，实际类型为 {actualType}");
        }




        /* /// <summary>
         /// 转为方法入参数据
         /// </summary>
         /// <returns></returns>
         public async Task<object> ToMethodArgData2(IFlowContext context)
         {

             var nodeModel = NodeModel;
             var env = nodeModel.Env;

             #region 流程运行上下文预设的参数
             if (context.TryGetParamsTempData(NodeModel.Guid, Index, out var data))
             {
                 return data;
             }
             #endregion

             #region 显然的流程基本类型
             // 返回运行环境
             if (typeof(IFlowEnvironment).IsAssignableFrom(DataType))
             {
                 return env;
             }
             // 返回流程上下文
             if (typeof(IFlowContext).IsAssignableFrom(DataType))
             {
                 return context;
             }
             // 返回流程上下文
             if (typeof(IFlowNode).IsAssignableFrom(DataType))
             {
                 return NodeModel;
             }
             // 显式设置的参数
             if (IsExplicitData && !DataValue.StartsWith("@", StringComparison.OrdinalIgnoreCase))
             {
                 return DataValue.ToConvertValueType(DataType); // 并非表达式，同时是显式设置的参数
             }
             #endregion





             *//*#region “枚举-类型”转换器
             if (ExplicitType is not null && ExplicitType.IsEnum && DataType != ExplicitType)
             {
                 var resultEnum = Enum.Parse(ExplicitType, DataValue);
                 // 获取绑定的类型
                 var type = EnumHelper.GetBoundValue(ExplicitType, resultEnum, attr => attr.Value);
                 if (type is Type enumBindType && !(enumBindType is null))
                 {
                     var value = nodeModel.Env.IOC.CreateObject(enumBindType);
                     return value;
                 }
             } 
             #endregion*//*

             // 需要获取预入参数据
             object inputParameter;
             #region （默认的）从运行时上游节点获取其返回值

             if (ArgDataSourceType == ConnectionArgSourceType.GetPreviousNodeData)
             {
                 var previousNode = context.GetPreviousNode(nodeModel.Guid);
                 if (previousNode is null)
                 {
                     inputParameter = null;
                 }
                 else
                 {
                     var flowData = context.GetFlowData(previousNode);
                     inputParameter = flowData.Value; // 当前传递的数据
                 }
             }
             else
             {


                 if(!env.TryGetNodeModel(ArgDataSourceNodeGuid, out var argSourceNodeModel))
                 {
                     throw new Exception($"[arg{Index}][{Name}][{DataType}]需要节点[{ArgDataSourceNodeGuid}]的参数，但节点不存在");
                 }

                 // 如果是公开的节点，需要判断上下文调用中是否存在流程接口节点
                 if (argSourceNodeModel.IsPublic)
                 {
                     var pnGuid = context.GetPreviousNode(NodeModel.Guid);
                     var pn = env.TryGetNodeModel(pnGuid, out var tmpNode) ? tmpNode : null;
                     if (pn.ControlType == NodeControlType.FlowCall)
                     {
                         argSourceNodeModel = pn;
                     }
                 }

                 if (ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeData)
                 {
                     var flowData = context.GetFlowData(argSourceNodeModel.Guid);
                     if(flowData is null)
                     {
                         inputParameter = null;
                     }
                     else
                     {

                         inputParameter = flowData.Value;
                     }
                 }
                 else if (ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeDataOfInvoke)
                 {
                     // 立刻调用对应节点获取数据。
                     var cts = new CancellationTokenSource();
                     var result = await argSourceNodeModel.ExecutingAsync(context, cts.Token);
                     cts?.Cancel();
                     cts?.Dispose();
                     inputParameter = result.Value;
                 }
                 else
                 {
                     throw new Exception("节点执行方法获取入参参数时，ConnectionArgSourceType枚举是意外的枚举值");
                 }
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
 */
        /// <summary>
        /// 转为字符串描述
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"[{this.Index}] {(string.IsNullOrWhiteSpace(this.Description) ? string.Empty : $"({this.Description})")}{this.Name} : {this.DataType?.GetFriendlyName()}";
        }
    }





}
