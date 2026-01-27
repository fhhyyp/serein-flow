using Microsoft.CodeAnalysis;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Model.Infos;
using Serein.NodeFlow.Model.Nodes;
using Serein.Script;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Serein.NodeFlow.Services
{
    /// <summary>
    /// 流程代码生成服务
    /// </summary>
    public class FlowCoreGenerateService
    {
        private readonly FlowModelService flowModelService;
        private readonly IFlowLibraryService flowLibraryService;

        /// <summary>
        /// 流程代码生成服务
        /// </summary>
        /// <param name="flowModelService"></param>
        /// <param name="flowLibraryService"></param>
        public FlowCoreGenerateService(FlowModelService flowModelService ,IFlowLibraryService flowLibraryService )
        {
            this.flowModelService = flowModelService;
            this.flowLibraryService = flowLibraryService;
        }

        /// <summary>
        /// 生成C#代码文件内容，包含流程调用树和节点方法
        /// </summary>
        /// <returns></returns>
        public string ToCsharpCoreFile()
        {
            StringBuilder stringBuilder = new StringBuilder();
            HashSet<Type> assemblyFlowClasss = new HashSet<Type>(); // 用于创建依赖注入项
            assemblyFlowClasss.Add(typeof(IFlowEnvironment)); // 调用树

            
            var flowNodes = flowModelService.GetAllNodeModel().ToArray();
            var flowCanvass = flowModelService.GetAllCanvasModel().ToArray();
            // 收集程序集信息
            foreach (var node in flowNodes)
            {
                var instanceType = node.MethodDetails.ActingInstanceType;
                if (instanceType is not null)
                {
                    assemblyFlowClasss.Add(instanceType);
                }
            }

            var scriptNodes = flowModelService.GetAllNodeModel().Where(n => n.ControlType == NodeControlType.Script).OfType<SingleScriptNode>().ToArray();
            GenerateScript_InitSereinScriptMethodInfos(scriptNodes); // 初始化脚本方法

            var flowCallNodes = flowModelService.GetAllNodeModel().Where(n => n.ControlType == NodeControlType.FlowCall).OfType<SingleFlowCallNode>().ToArray();
            GenerateFlowApi_InitFlowApiMethodInfos(flowCallNodes); // 初始化流程接口信息

            var globalDataNodes = flowModelService.GetAllNodeModel().Where(n => n.ControlType == NodeControlType.GlobalData).OfType<SingleGlobalDataNode>().ToArray();
            GenerateGlobalData_InitSereinGlobalDataInfos(globalDataNodes); // 初始化全局数据信息


            GenerateFlowApi_InterfaceAndImpleClass(stringBuilder); // 生成接口类
            GenerateFlowApi_ApiParamClass(stringBuilder); // 生成接口参数类
            string flowTemplateClassName = $"FlowTemplate"; // 类名
            string flowApiInterfaceName = $"IFlowApiInvoke"; // 类名
            stringBuilder.AppendCode(0, $"public class {flowTemplateClassName} : {flowApiInterfaceName}, global::{typeof(IFlowCallTree).FullName}");
            stringBuilder.AppendCode(0, $"{{");

            // 生成 IFlowCallTree 接口
            var listNodes = $"global::System.Collections.Generic.List<{typeof(CallNode).FullName}>";
            stringBuilder.AppendCode(1, $"public {listNodes} {nameof(IFlowCallTree.StartNodes)} {{ get; }} = new {listNodes}();");
            stringBuilder.AppendCode(1, $"public {listNodes} {nameof(IFlowCallTree.GlobalFlipflopNodes)} {{get; }} = new {listNodes}();");


            GenerateCtor(stringBuilder, flowTemplateClassName, assemblyFlowClasss); // 生成构造方法
            GenerateInitMethod(stringBuilder); // 生成初始化方法
            GenerateCallTree(stringBuilder, flowNodes, flowCanvass); // 生成调用树
            Generate_InitAndStart(stringBuilder); // 生成 InitAndStartAsync
            GenerateNodeIndexLookup(stringBuilder, flowTemplateClassName, flowNodes); // 初始化节点缓存

            // 生成每个节点的方法
            foreach (var node in flowNodes)
            {
                GenerateMethod(stringBuilder, node); // 生成每个节点的方法
            }

            // 生成实现流程接口的实现方法
            var flowApiInfos = _flowApiMethodInfos.Values.ToArray();
            foreach (var info in flowApiInfos)
            {
                stringBuilder.AppendCode(2, info.ToObjPoolSignature());
                stringBuilder.AppendCode(2, info.ToImpleMethodSignature(FlowApiMethodInfo.ParamType.Defute));
                stringBuilder.AppendCode(2, info.ToImpleMethodSignature(FlowApiMethodInfo.ParamType.HasToken));
                stringBuilder.AppendCode(2, info.ToImpleMethodSignature(FlowApiMethodInfo.ParamType.HasContextAndToken));
            }

            stringBuilder.AppendCode(0, $"}}");


            // 载入脚本节点转换的C#代码（载入类）
            var scriptInfos = _scriptMethodInfos.Values.ToArray();
            foreach (var info in scriptInfos)
            {
                stringBuilder.AppendCode(2, info.CsharpCode);
            }

            // 载入全局数据节点转换的C#代码（载入类）
            GenerateGlobalData_ToClass(stringBuilder);

            return stringBuilder.ToString();
        }

        /// <summary>
        /// 生成构造函数代码
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="className"></param>
        /// <param name="assemblyFlowClasss"></param>
        /// <returns></returns>
        private void GenerateCtor(StringBuilder sb, string className, HashSet<Type> assemblyFlowClasss)
        {
            if (assemblyFlowClasss.Count == 0)
            {
                return;
            }
            var instanceTypes = assemblyFlowClasss.Where(x => !IsStaticClass(x)).ToArray();

            for (int index = 0; index < instanceTypes.Length; index++)
            {
                var type = instanceTypes[index];
                var ctor_parms_name = type.ToCamelCase();
                sb.AppendCode(2, $"private readonly global::{type.FullName} {ctor_parms_name};");
            }

            sb.AppendLine();

            sb.AppendCode(2, $"public {className}(", false);
            for (int index = 0; index < instanceTypes.Length; index++)
            {
                var type = instanceTypes[index];
                var ctor_parms_name = type.ToCamelCase();
                sb.Append($"global::{type.FullName} {ctor_parms_name}{(index < instanceTypes.Length - 1 ? "," : "")}");
            }
            sb.AppendCode(0, $")");
            sb.AppendCode(2, $"{{");
            for (int index = 0; index < instanceTypes.Length; index++)
            {
                var type = instanceTypes[index];
                var ctor_parms_name = type.ToCamelCase();
                sb.AppendCode(3, $"this.{ctor_parms_name} = {ctor_parms_name};");
            }
            sb.AppendLine();
            sb.AppendCode(3, $"Init();"); // 初始化调用树
            sb.AppendCode(2, $"}}");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成方法调用逻辑
        /// </summary>
        /// <param name="sb_main"></param>
        /// <param name="flowNode"></param>
        /// <exception cref="Exception"></exception>
        private void GenerateMethod(StringBuilder sb_main, IFlowNode flowNode)
        {
            string? flowContextTypeName = typeof(IFlowContext).FullName;
            string? flowContext = nameof(flowContext);

            if (flowNode.ControlType == NodeControlType.Action && flowNode is SingleActionNode actionNode)
            {
                CreateMethodCore_ActionOrFliplop(sb_main, actionNode, flowContextTypeName, flowContext);
            }
            else if (flowNode.ControlType == NodeControlType.Flipflop && flowNode is SingleFlipflopNode flipflopNode)
            {
                CreateMethodCore_ActionOrFliplop(sb_main, flipflopNode, flowContextTypeName, flowContext);
            }
            else if (flowNode.ControlType == NodeControlType.Script && flowNode is SingleScriptNode singleScriptNode)
            {
                CreateMethodCore_Script(sb_main, singleScriptNode, flowContextTypeName, flowContext);
            }
            else if (flowNode.ControlType == NodeControlType.GlobalData && flowNode is SingleGlobalDataNode globalDataNode)
            {
                CreateMethodCore_GlobalData(sb_main, globalDataNode, flowContextTypeName, flowContext);
            }
            else if (flowNode.ControlType == NodeControlType.UI)
            {
            }
            else if (flowNode.ControlType == NodeControlType.ExpCondition)
            {

            }
            else if (flowNode.ControlType == NodeControlType.ExpOp)
            {
            }
            else if (flowNode.ControlType == NodeControlType.FlowCall && flowNode is SingleFlowCallNode flowCallNode)
            {
                CreateMethodCore_FlowCall(sb_main, flowCallNode, flowContextTypeName, flowContext);
            }

            return;
            throw new Exception("无法为该节点生成调用逻辑");
        }

        /// <summary>
        /// 生成初始化方法（用于执行构造函数中无法完成的操作）
        /// </summary>
        /// <param name="sb"></param>
        private void GenerateInitMethod(StringBuilder sb)
        {
            sb.AppendCode(2, $"private void Init()");
            sb.AppendCode(2, $"{{");
            sb.AppendCode(3, $"{nameof(GenerateCallTree)}(); // 初始化调用树"); // 初始化调用树
            sb.AppendCode(2, $"}}");
        }

        /// <summary>
        /// 生成调用树
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="flowNodes"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private void GenerateCallTree(StringBuilder sb, IFlowNode[] flowNodes, FlowCanvasDetails[] flowCanvass)
        {
            //  Get("0fa6985b-4b63-4499-80b2-76401669292d").AddChildNodeSucceed(Get("acdbe7ea-eb27-4a3e-9cc9-c48f642ee4f5"));
            sb.AppendCode(2, $"private void {nameof(GenerateCallTree)}()");
            sb.AppendCode(2, $"{{");
            #region 设置节点回调
            foreach (var node in flowNodes)
            {
                var nodeMethod = node.ToNodeMethodName(); // 节点对应的方法名称
                if (node.ControlType == NodeControlType.Action
                    || node.ControlType == NodeControlType.Flipflop
                    || node.ControlType == NodeControlType.FlowCall
                    || node.ControlType == NodeControlType.Script
                    || node.ControlType == NodeControlType.GlobalData
                    )
                {
                    var md = node.MethodDetails;
                    var methodTips = string.IsNullOrWhiteSpace(md.MethodAnotherName) ? md.MethodName : md.MethodAnotherName;
                    sb.AppendCode(3, $"Get(\"{node.Guid}\").SetAction({nodeMethod}); // [{node.ControlType}] {methodTips}");
                }
                else
                {
                    sb.AppendCode(3, $"// Get(\"{node.Guid}\").SetAction({nodeMethod}); // 暂未实现节点类型为[{node.ControlType}]的方法生成");

                }

            }

            #endregion
            #region 设置调用顺序
            var cts = NodeStaticConfig.ConnectionTypes;
            foreach (var node in flowNodes)
            {

                if (node.ControlType == NodeControlType.FlowCall && node is SingleFlowCallNode flowCallNode)
                {
                    foreach (var ct in cts)
                    {
                        var childNodes = flowCallNode.TargetNode.SuccessorNodes[ct];
                        var AddChildNodeMethodName = ct switch
                        {
                            ConnectionInvokeType.IsSucceed => nameof(CallNode.AddChildNodeSucceed),
                            ConnectionInvokeType.IsFail => nameof(CallNode.AddChildNodeFail),
                            ConnectionInvokeType.IsError => nameof(CallNode.AddChildNodeError),
                            ConnectionInvokeType.Upstream => nameof(CallNode.AddChildNodeUpstream),
                            _ => throw new ArgumentOutOfRangeException(nameof(ct), ct, null)
                        };
                        foreach (var childNode in childNodes)
                        {
                            sb.AppendCode(3, $"Get(\"{node.Guid}\").{AddChildNodeMethodName}(Get(\"{childNode.Guid}\")); // 流程节点调用");
                        }
                    }
                }
                else
                {
                    var nodeMethod = node.ToNodeMethodName(); // 节点对应的方法名称
                    foreach (var ct in cts)
                    {
                        var childNodes = node.SuccessorNodes[ct];
                        var AddChildNodeMethodName = ct switch
                        {
                            ConnectionInvokeType.IsSucceed => nameof(CallNode.AddChildNodeSucceed),
                            ConnectionInvokeType.IsFail => nameof(CallNode.AddChildNodeFail),
                            ConnectionInvokeType.IsError => nameof(CallNode.AddChildNodeError),
                            ConnectionInvokeType.Upstream => nameof(CallNode.AddChildNodeUpstream),
                            _ => throw new ArgumentOutOfRangeException(nameof(ct), ct, null)
                        };
                        foreach (var childNode in childNodes)
                        {
                            sb.AppendCode(3, $"Get(\"{node.Guid}\").{AddChildNodeMethodName}(Get(\"{childNode.Guid}\"));");
                        }
                    }

                }

            }
            #endregion

            #region 实现接口
            var startNodeGuids = flowCanvass.Where(canvas => canvas.StartNode is not null).Select(canvas => canvas.StartNode!.Guid).ToList();
            foreach (var startNodeGuid in startNodeGuids)
            {
                sb.AppendCode(3, $"{nameof(IFlowCallTree.StartNodes)}.Add(Get(\"{startNodeGuid}\")); // 添加起始节点");
            }

            var flipflopNodeGuids = flowNodes.Where(node => node.ControlType == NodeControlType.Flipflop)
                                     .OfType<SingleFlipflopNode>()
                                     .Where(node => node.IsRoot())
                                     .ToList();
            foreach (var flipflopNodeGuid in flipflopNodeGuids)
            {
                sb.AppendCode(3, $"{nameof(IFlowCallTree.GlobalFlipflopNodes)}.Add(Get(\"{flipflopNodeGuid.Guid}\")); // 添加全局触发器节点");
            } 
            #endregion
            sb.AppendCode(2, $"}}");
            sb.AppendLine();



            /*string? dynamicContextTypeName = typeof(IDynamicContext).FullName;
            string? flowContext = nameof(flowContext);
            var callTreeType = typeof(IFlowCallTree);
            var callTreeName = GetCamelCase(callTreeType);
            //var canvasGuid = flowCanvas.Guid;
            //var startNodeGuid = flowCanvas.StartNode.Guid;

            sb.AppendCode(2, $"private void {nameof(GenerateCallTree)}()");
            sb.AppendCode(2, $"{{");
            //sb.AppendCode(3, $"global::{callTreeType.FullName} {callTreeName} = new global::{callTreeType.FullName}()\";");

            // 注册节点
           *//* foreach (var node in flowCanvas.Nodes)
            {
                var nodeMethod = GetNodeMethodName(node);
                var call = $"{flowContext} => {nodeMethod}({flowContext})";
                sb.AppendCode(3, $"{callTreeName}.{nameof(FlowCallTree.AddCallNode)}(\"{node.Guid}\", {call});");
            }*//*

            sb.AppendLine();
            foreach (var node in flowNodes)
            {
                var nodeMethod = GetNodeMethodName(node);
                var cts = NodeStaticConfig.ConnectionTypes;
                foreach (var ct in cts)
                {
                    var childNodes = node.SuccessorNodes[ct];
                    var addType = ct switch
                    {
                        ConnectionInvokeType.IsSucceed => nameof(CallNode.AddChildNodeSucceed),
                        ConnectionInvokeType.IsFail => nameof(CallNode.AddChildNodeFail),
                        ConnectionInvokeType.IsError => nameof(CallNode.AddChildNodeError),
                        ConnectionInvokeType.Upstream => nameof(CallNode.AddChildNodeUpstream),
                        _ => throw new ArgumentOutOfRangeException(nameof(ct), ct, null)
                    };
                    foreach (var childNode in childNodes)
                    {
                        sb.AppendCode(3, $"{callTreeName}[\"{node.Guid}\"].{addType}(\"{childNode.Guid}\");");
                    }
                }

            }
            sb.AppendCode(2, $"}}");
            sb.AppendLine();*/
        }

        /// <summary>
        /// 生成节点索引查找方法
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="className"></param>
        /// <param name="flowNodes"></param>
        private void GenerateNodeIndexLookup(StringBuilder sb, string className, IFlowNode[] flowNodes)
        {
            // 初始化Id
            nodeIdMap.Clear();
            for (int index = 0; index < flowNodes.Length; index++)
            {
                var flowNode = flowNodes[index];
                GetNodeId(flowNode);
            }

            var valueArrayName = "_values";

            // 生成 _values 
            sb.AppendCode(2, $"private readonly static global::Serein.Library.CallNode[] {valueArrayName} = new global::Serein.Library.CallNode[{flowNodes.Length}];");

            /*sb.AppendCode(2, $"private readonly static global::System.String[] _keys = new global::System.String[]");
            sb.AppendCode(2, $"{{");
            for (int index = 0; index < flowNodes.Length; index++)
            {
                var flowNode = flowNodes[index];
                sb.AppendCode(3, $"\"{flowNode.Guid}\",  // {index} : {flowNode.MethodDetails.MethodName}");
            }
            sb.AppendCode(2, $"}};");*/

            // 生成静态构造函数
            sb.AppendCode(2, $"static {className}()");
            sb.AppendCode(2, $"{{");
            for (int index = 0; index < flowNodes.Length; index++)
            {
                var flowNode = flowNodes[index];
                sb.AppendCode(3, $"{valueArrayName}[{index}] = new  global::Serein.Library.CallNode(\"{flowNode.Guid}\"); // {index} : {flowNode.MethodDetails.MethodName}");
            }
            sb.AppendCode(2, $"}}");

            // 初始化 Get 函数
            var nodeIndexName = "node_index";
            //sb.AppendCode(2, $" [MethodImpl(MethodImplOptions.AggressiveInlining)]"); // 内联优化
            sb.AppendCode(2, $"public global::Serein.Library.CallNode {nameof(IFlowCallTree.Get)}( global::System.String key)");
            sb.AppendCode(2, $"{{");
            sb.AppendCode(3, $"global::System.Int32 {nodeIndexName};");
            sb.AppendCode(3, $"switch (key)");
            sb.AppendCode(3, $"{{");

            for (int index = 0; index < flowNodes.Length; index++)
            {
                var flowNode = flowNodes[index];
                sb.AppendCode(4, $"case \"{flowNode.Guid}\":");
                sb.AppendCode(5, $"{nodeIndexName} = {index};");
                sb.AppendCode(5, $"break;");
            }
            sb.AppendCode(4, $"default:");
            sb.AppendCode(4, $"{nodeIndexName} = -1;");
            sb.AppendCode(5, $"break;");
            sb.AppendCode(3, $"}}");
            sb.AppendCode(3, $"return {valueArrayName}[{nodeIndexName}];");
            sb.AppendCode(2, $"}}");
        }

        private void Generate_InitAndStart(StringBuilder sb)
        {
            string value =
"""
/// <summary>
/// 初始化并启动流程控制器，遍历所有的起始节点并启动对应的流程，同时处理全局触发器节点。
/// </summary>
/// <returns></returns>
public async global::System.Threading.Tasks.Task InitAndStartAsync(global::System.Threading.CancellationToken token)
{
    var startNodes = StartNodes.ToArray();
    foreach (var startNode in startNodes)
    {
        await flowEnvironment.FlowControl.StartFlowAsync(startNode.Guid);
    }
    var globalFlipflopNodes = GlobalFlipflopNodes.ToArray();
    var tasks = globalFlipflopNodes.Select(async node =>
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await flowEnvironment.FlowControl.StartFlowAsync(node.Guid);
            }
            catch (global::Serein.Library.FlipflopException ex)
            {
                if (ex.Type == global::Serein.Library.FlipflopException.CancelClass.CancelFlow)
                    break;
            }
        }
    });
    await Task.WhenAll(tasks);
}
""";
            sb.AppendLine(value);
        }

        #region 节点方法生成

        /// <summary>
        /// 生成[Action]节点的方法调用
        /// </summary>
        /// <param name="sb_main"></param>
        /// <param name="actionNode"></param>
        /// <param name="flowContextTypeName"></param>
        /// <param name="flowContext"></param>
        /// <exception cref="Exception"></exception>
        private void CreateMethodCore_ActionOrFliplop(StringBuilder sb_main, IFlowNode actionNode, string? flowContextTypeName, string flowContext)
        {
            if (!flowLibraryService.TryGetMethodInfo(actionNode.MethodDetails.AssemblyName,
                                                                                actionNode.MethodDetails.MethodName,
                                                                                out var methodInfo) || methodInfo is null)
            {
                return;
            }

            var isRootNode = actionNode.IsRoot();

            var instanceType = actionNode.MethodDetails.ActingInstanceType;
            var returnType = methodInfo.ReturnType;

            var instanceName = instanceType.ToCamelCase();// $"instance_{instanceType.Name}";

            var instanceTypeFullName = instanceType.FullName;
            var returnTypeFullName = returnType == typeof(void) ? "void" : returnType.FullName;

            #region 方法内部逻辑
            StringBuilder sb_invoke_login = new StringBuilder();
            if (actionNode.MethodDetails is null) return;
            var param = methodInfo.GetParameters();
            var md = actionNode.MethodDetails;
            var pds = actionNode.MethodDetails.ParameterDetailss;
            if (param is null) return;
            if (pds is null) return;

            for (int index = 0; index < pds.Length; index++)
            {
                ParameterDetails? pd = pds[index];
                ParameterInfo parameterInfo = pd.IsParams ? param[md.ParamsArgIndex] : param[index];
                var paramtTypeFullName = pd.IsParams ? parameterInfo.ParameterType.GetElementType().GetFriendlyName() : parameterInfo.ParameterType.GetFriendlyName();

                if (pd.IsExplicitData)
                {
                    // 只能是 数值、 文本、枚举， 才能作为显式参数
                    if (parameterInfo.ParameterType.IsValueType)
                    {
                        if (parameterInfo.ParameterType.IsEnum)
                        {
                            sb_invoke_login.AppendCode(3, $"global::{paramtTypeFullName} value{index} = global::{paramtTypeFullName}.{pd.DataValue}; // 获取当前节点的上一节点数据");
                        }
                        else
                        {
                            var value = pd.DataValue.ToConvertValueType(parameterInfo.ParameterType);
                            sb_invoke_login.AppendCode(3, $"global::{paramtTypeFullName} value{index} = (global::{paramtTypeFullName}){value}; // 获取当前节点的上一节点数据");

                        }
                    }
                    else /*if (parameterInfo.ParameterType == typeof(string))*/
                    {
                        var dataString = EscapeForCSharpString(pd.DataValue);
                        sb_invoke_login.AppendCode(3, $"global::{paramtTypeFullName} value{index} = \"{dataString}\"; // 获取当前节点的上一节点数据");
                    }
                    
                }
                else
                {
                    #region 非显式设置的参数以正常方式获取
                    if (pd.ArgDataSourceType == ConnectionArgSourceType.GetPreviousNodeData)
                    {
                        
                        var valueType = pd.IsParams ? $"global::{pd.DataType.GetFriendlyName()}" : $"global::{paramtTypeFullName}";
                        if (typeof(IFlowContext).IsAssignableFrom(pd.DataType))
                        {
                            sb_invoke_login.AppendCode(3, $"{valueType} value{index} = {flowContext}; // 使用流程上下文");
                        }
                        else
                        {
                            var previousNode = $"previousNode{index}";
                            sb_invoke_login.AppendCode(3, $"global::System.String {previousNode} = {flowContext}.GetPreviousNode(\"{actionNode.Guid}\");"); // 获取运行时上一节点Guid
                            sb_invoke_login.AppendCode(3, $"{valueType} value{index} = {previousNode} == null ? default : ({valueType}){flowContext}.{nameof(IFlowContext.GetFlowData)}({previousNode}).Value; // 获取运行时上一节点的数据");
                        }
                    }
                    else if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeData)
                    {
                        if (flowModelService.TryGetNodeModel(pd.ArgDataSourceNodeGuid, out var otherNode))
                        {
                            var valueType = pd.IsParams ? $"global::{pd.DataType.FullName}" : $"global::{paramtTypeFullName}";
                            var otherNodeReturnType = otherNode.MethodDetails.ReturnType;
                            if (otherNodeReturnType == typeof(object))
                            {
                                sb_invoke_login.AppendCode(3, $"{valueType} value{index} = ({valueType}){flowContext}.{nameof(IFlowContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
                            }
                            else if (pd.DataType.IsAssignableFrom(otherNodeReturnType))
                            {
                                sb_invoke_login.AppendCode(3, $"{valueType} value{index} = ({valueType}){flowContext}.{nameof(IFlowContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
                            }
                            else
                            {
                                // 获取的数据无法转换为目标方法入参类型
                                throw new Exception("获取的数据无法转换为目标方法入参类型");
                            }
                        }
                        else
                        {
                            // 指定了Guid，但项目中不存在对应的节点，需要抛出异常
                            throw new Exception("指定了Guid，但项目中不存在对应的节点");
                        }
                    }
                    else if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeDataOfInvoke)
                    {
                        if (flowModelService.TryGetNodeModel(pd.ArgDataSourceNodeGuid, out var otherNode)) // 获取指定节点
                        {
                            var otherNodeReturnType = otherNode.MethodDetails.ReturnType;
                            var valueType = pd.IsParams ? $"global::{pd.DataType.FullName}" : $"global::{otherNode.MethodDetails.ReturnType.FullName}";
                            if (otherNodeReturnType == typeof(object))
                            {
                                sb_invoke_login.AppendCode(3, $"{valueType} value{index} = ({valueType}){flowContext}.{nameof(IFlowContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
                            }
                            else if (pd.DataType.IsAssignableFrom(otherNodeReturnType))
                            {
                                if (typeof(Task).IsAssignableFrom(otherNode.MethodDetails.ReturnType))
                                {
                                    sb_invoke_login.Append("await ");
                                }
                                sb_invoke_login.AppendCode(3, $"{otherNode.ToNodeMethodName()}({flowContext}); // 需要立即调用指定方法");
                                sb_invoke_login.AppendCode(3, $"{valueType} value{index} = ({valueType}){flowContext}.{nameof(IFlowContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
                            }
                            else
                            {
                                // 获取的数据无法转换为目标方法入参类型
                                throw new Exception("获取的数据无法转换为目标方法入参类型");
                            }

                        }
                        else
                        {
                            // 指定了Guid，但项目中不存在对应的节点，需要抛出异常
                            throw new Exception("指定了Guid，但项目中不存在对应的节点");
                        }
                    }
                    #endregion

                }
            }

            if (methodInfo.ReturnType == typeof(void) || methodInfo.ReturnType == typeof(Task))
            {
                if (methodInfo.IsStatic)
                {

                }
                // 调用无返回值方法
                // 如果目标方法是静态的，则以“命名空间.类.方法”形式调用，否则以“实例.方法”形式调用
                var invokeFunctionContext = methodInfo.IsStatic ? $"{instanceType}.{methodInfo.Name}" : $"{instanceName}.{methodInfo.Name}";
                // 如果目标方法是异步的，则自动 await 进行等待
                invokeFunctionContext = md.IsAsync ? $"await {invokeFunctionContext}" : invokeFunctionContext;
                sb_invoke_login.AppendCode(3, $"{invokeFunctionContext}(", false);
                for (int index = 0; index < pds.Length; index++)
                {
                    sb_invoke_login.Append($"{(index == 0 ? "" : ",")}value{index}");
                }
                sb_invoke_login.AppendCode(0, $"); // 调用方法 {md.MethodAnotherName}");
            }
            else
            {
                // 调用有返回值方法
                // 如果目标方法是静态的，则以“命名空间.类.方法”形式调用，否则以“实例.方法”形式调用
                var invokeFunctionContext = methodInfo.IsStatic ? $"global::{instanceType}.{methodInfo.Name}" : $"{instanceName}.{methodInfo.Name}";
                // 如果目标方法是异步的，则自动 await 进行等待
                invokeFunctionContext = md.IsAsync ? $"await {invokeFunctionContext}" : invokeFunctionContext;
                sb_invoke_login.AppendCode(3, $"var result = {invokeFunctionContext}(", false);
                for (int index = 0; index < pds.Length; index++)
                {
                    sb_invoke_login.Append($"{(index == 0 ? "" : ",")}value{index}");
                }
                sb_invoke_login.AppendCode(0, $"); // 调用方法 {md.MethodAnotherName}");

                sb_invoke_login.AppendCode(3, $"{flowContext}.{nameof(IFlowContext.AddOrUpdate)}(\"{actionNode.Guid}\", result);", false); // 更新数据
                //sb_invoke_login.AppendCode(3, $"return result;", false);
            }
            #endregion

            // global::{returnTypeFullName}

            var resultTypeName = actionNode.MethodDetails.IsAsync ? "async Task" : "void";

            sb_main.AppendCode(2, $"[Description(\"{instanceTypeFullName}.{methodInfo.Name}\")]");
            sb_main.AppendCode(2, $"private {resultTypeName} {actionNode.ToNodeMethodName()}(global::{flowContextTypeName} {flowContext})");
            sb_main.AppendCode(2, $"{{");
            sb_main.AppendCode(0, sb_invoke_login.ToString());
            sb_main.AppendCode(2, $"}}"); // 方法结束
            sb_main.AppendLine(); // 方法结束

        }

        /// <summary>
        /// 生成[FlowCall]节点的方法调用
        /// </summary>
        /// <param name="sb_main"></param>
        /// <param name="flowCallNode"></param>
        /// <param name="flowContextTypeName"></param>
        /// <param name="flowContext"></param>
        /// <exception cref="Exception"></exception>
        private void CreateMethodCore_FlowCall(StringBuilder sb_main, SingleFlowCallNode flowCallNode, string? flowContextTypeName, string flowContext)
        {
            if (!_flowApiMethodInfos.TryGetValue(flowCallNode, out var flowApiMethodInfo))
            {
                return;
            }

            if (flowCallNode.TargetNode is SingleScriptNode singleScriptNode)
            {
                if (!_scriptMethodInfos.TryGetValue(singleScriptNode, out var scriptMethodInfo))
                {
                    return;
                }

                var instanceType = flowCallNode.MethodDetails.ActingInstanceType;
                var returnType = singleScriptNode.MethodDetails.ReturnType;

                var returnTypeFullName = returnType == typeof(void) ? "void" : returnType.FullName;

                #region 方法内部逻辑
                StringBuilder sb_invoke_login = new StringBuilder();

                if (flowCallNode.MethodDetails is null) return;
                var md = flowCallNode.MethodDetails;
                var pds = flowCallNode.MethodDetails.ParameterDetailss;
                if (pds is null) return;

                var flowDataName = $"flowData{flowApiMethodInfo.ApiMethodName}";
                var apiData = $"apiData{flowApiMethodInfo.ApiMethodName}";
                sb_invoke_login.AppendCode(3, $"global::{typeof(object).FullName} {flowDataName} = {flowContext}.GetFlowData(\"{flowApiMethodInfo.ApiMethodName}\").Value;");
                sb_invoke_login.AppendCode(3, $"if({flowDataName} is {flowApiMethodInfo.ParamTypeName} {apiData})");
                sb_invoke_login.AppendCode(3, $"{{");
                foreach (var info in flowApiMethodInfo.ParamInfos)
                {
                    sb_invoke_login.AppendCode(4, $"global::{info.Type.FullName} {info.ParamName} = {apiData}.{info.ParamName.ToPascalCase()}; ");
                }
                var invokeParamContext = string.Join(", ", flowApiMethodInfo.ParamInfos.Select(x => x.ParamName));
                if (flowApiMethodInfo.IsVoid)
                {
                    // 调用无返回值方法
                    var invokeFunctionContext = $"{scriptMethodInfo.ClassName}.{scriptMethodInfo.MethodName}";
                    // 如果目标方法是异步的，则自动 await 进行等待
                    sb_invoke_login.AppendCode(4, $"{(md.IsAsync ? $"await" : string.Empty)} {invokeFunctionContext}({invokeParamContext});");
                }
                else
                {
                    // 调用有返回值方法
                    var resultName = $"result{flowApiMethodInfo.ApiMethodName}";
                    var invokeFunctionContext = $"{scriptMethodInfo.ClassName}.{scriptMethodInfo.MethodName}";
                    // 如果目标方法是异步的，则自动 await 进行等待
                    sb_invoke_login.AppendCode(4, $"var {resultName} = {(md.IsAsync ? $"await" : string.Empty)} {invokeFunctionContext}({invokeParamContext});");

                    sb_invoke_login.AppendCode(4, $"{flowContext}.{nameof(IFlowContext.AddOrUpdate)}(\"{flowCallNode.TargetNode.Guid}\", {resultName});"); // 更新数据
                    sb_invoke_login.AppendCode(4, $"{flowApiMethodInfo.ObjPoolName}.Return({apiData});"); // 归还到对象池
                                                                                                          //sb_invoke_login.AppendCode(3, $"return result;", false);
                }
                sb_invoke_login.AppendCode(3, $"}}");
                sb_invoke_login.AppendCode(3, $"else");
                sb_invoke_login.AppendCode(3, $"{{");
                sb_invoke_login.AppendCode(4, $"throw new Exception(\"接口参数类型异常\");");
                sb_invoke_login.AppendCode(3, $"}}");




                #endregion


                var resultTypeName = flowCallNode.MethodDetails.IsAsync ? "async Task" : "void";

                sb_main.AppendCode(2, $"[Description(\"脚本节点接口\")]");
                sb_main.AppendCode(2, $"private {resultTypeName} {flowCallNode.ToNodeMethodName()}(global::{flowContextTypeName} {flowContext})");
                sb_main.AppendCode(2, $"{{");
                sb_main.AppendCode(0, sb_invoke_login.ToString());
                sb_main.AppendCode(2, $"}} "); // 方法结束

                sb_main.AppendLine(); // 方法结束
            }
            else
            {
                if (!flowLibraryService.TryGetMethodInfo(flowCallNode.MethodDetails.AssemblyName,
                                                     flowCallNode.MethodDetails.MethodName,
                                                     out var methodInfo) || methodInfo is null)
                {
                    return;
                }

                var isRootNode = flowCallNode.IsRoot();

                var instanceType = flowCallNode.MethodDetails.ActingInstanceType;
                var returnType = methodInfo.ReturnType;

                var instanceName = instanceType.ToCamelCase();// $"instance_{instanceType.Name}";

                var instanceTypeFullName = instanceType.FullName;
                var returnTypeFullName = returnType == typeof(void) ? "void" : returnType.FullName;

                #region 方法内部逻辑
                StringBuilder sb_invoke_login = new StringBuilder();

                if (flowCallNode.MethodDetails is null) return;
                //var param = methodInfo.GetParameters();
                var md = flowCallNode.MethodDetails;
                var pds = flowCallNode.MethodDetails.ParameterDetailss;
                //if (param is null) return;
                if (pds is null) return;

                /* for (int index = 0; index < pds.Length; index++)
                 {
                     ParameterDetails? pd = pds[index];
                     ParameterInfo parameterInfo = param[index];
                     var paramtTypeFullName = parameterInfo.ParameterType.FullName;
                 }*/

                var flowDataName = $"flowData{flowApiMethodInfo.ApiMethodName}";
                var apiData = $"apiData{flowApiMethodInfo.ApiMethodName}";
                sb_invoke_login.AppendCode(3, $"global::{typeof(object).FullName} {flowDataName} = {flowContext}.GetFlowData(\"{flowApiMethodInfo.ApiMethodName}\").Value;");
                sb_invoke_login.AppendCode(3, $"if({flowDataName} is {flowApiMethodInfo.ParamTypeName} {apiData})");
                sb_invoke_login.AppendCode(3, $"{{");
                foreach (var info in flowApiMethodInfo.ParamInfos)
                {
                    sb_invoke_login.AppendCode(4, $"global::{info.Type.FullName} {info.ParamName} = {apiData}.{info.ParamName.ToPascalCase()}; ");
                }
                var invokeParamContext = string.Join(", ", flowApiMethodInfo.ParamInfos.Select(x => x.ParamName));
                if (flowApiMethodInfo.IsVoid)
                {
                    // 调用无返回值方法
                    // 如果目标方法是静态的，则以“命名空间.类.方法”形式调用，否则以“实例.方法”形式调用
                    var invokeFunctionContext = methodInfo.IsStatic ? $"global::{instanceType}.{methodInfo.Name}" : $"{instanceName}.{methodInfo.Name}";
                    // 如果目标方法是异步的，则自动 await 进行等待
                    sb_invoke_login.AppendCode(4, $"{(md.IsAsync ? $"await" : string.Empty)} {invokeFunctionContext}({invokeParamContext});");
                }
                else
                {
                    // 调用有返回值方法
                    var resultName = $"result{flowApiMethodInfo.ApiMethodName}";
                    // 如果目标方法是静态的，则以“命名空间.类.方法”形式调用，否则以“实例.方法”形式调用
                    var invokeFunctionContext = methodInfo.IsStatic ? $"global::{instanceType}.{methodInfo.Name}" : $"{instanceName}.{methodInfo.Name}";
                    // 如果目标方法是异步的，则自动 await 进行等待
                    sb_invoke_login.AppendCode(4, $"var {resultName} = {(md.IsAsync ? $"await" : string.Empty)} {invokeFunctionContext}({invokeParamContext});");

                    sb_invoke_login.AppendCode(4, $"{flowContext}.{nameof(IFlowContext.AddOrUpdate)}(\"{flowCallNode.TargetNode.Guid}\", {resultName});"); // 更新数据
                    sb_invoke_login.AppendCode(4, $"{flowApiMethodInfo.ObjPoolName}.Return({apiData});"); // 归还到对象池
                                                                                                          //sb_invoke_login.AppendCode(3, $"return result;", false);
                }
                sb_invoke_login.AppendCode(3, $"}}");
                sb_invoke_login.AppendCode(3, $"else");
                sb_invoke_login.AppendCode(3, $"{{");
                sb_invoke_login.AppendCode(4, $"throw new Exception(\"接口参数类型异常\");");
                sb_invoke_login.AppendCode(3, $"}}");




                #endregion


                var resultTypeName = flowCallNode.MethodDetails.IsAsync ? "async Task" : "void";

                sb_main.AppendCode(2, $"[Description(\"{instanceTypeFullName}.{methodInfo.Name}\")]");
                sb_main.AppendCode(2, $"private {resultTypeName} {flowCallNode.ToNodeMethodName()}(global::{flowContextTypeName} {flowContext})");
                sb_main.AppendCode(2, $"{{");
                sb_main.AppendCode(0, sb_invoke_login.ToString());
                sb_main.AppendCode(2, $"}} "); // 方法结束

                sb_main.AppendLine(); // 方法结束
            }


        }

        /// <summary>
        /// 生成[Script]节点的方法调用
        /// </summary>
        /// <param name="sb_main"></param>
        /// <param name="singleScriptNode"></param>
        /// <param name="flowContextTypeName"></param>
        /// <param name="flowContext"></param>
        private void CreateMethodCore_Script(StringBuilder sb_main, SingleScriptNode singleScriptNode, string? flowContextTypeName, string flowContext)
        {

            if (!_scriptMethodInfos.TryGetValue(singleScriptNode, out var scriptMethodInfo))
            {
                return;
            }

            var isRootNode = singleScriptNode.IsRoot();


            var returnType = scriptMethodInfo.ReturnType;
            var returnTypeFullName = returnType == typeof(void) ? "void" : returnType.FullName;

            #region 方法内部逻辑
            StringBuilder sb_invoke_login = new StringBuilder();
            if (singleScriptNode.MethodDetails is null) return;
            var param = scriptMethodInfo.ParamInfos;
            var md = singleScriptNode.MethodDetails;
            var pds = singleScriptNode.MethodDetails.ParameterDetailss;
            if (param is null) return;
            if (pds is null) return;

            for (int index = 0; index < pds.Length; index++)
            {
                ParameterDetails? pd = pds[index];
                SereinScriptMethodInfo.SereinScriptParamInfo parameterInfo = param[index];
                var paramtTypeFullName = parameterInfo.ParameterType.FullName;

                if (pd.IsExplicitData)
                {
                    // 只能是 数值、 文本、枚举， 才能作为显式参数
                    if (parameterInfo.ParameterType.IsValueType)
                    {
                        if (parameterInfo.ParameterType.IsEnum)
                        {
                            sb_invoke_login.AppendCode(3, $"global::{paramtTypeFullName} value{index} = global::{paramtTypeFullName}.{pd.DataValue}; // 获取当前节点的上一节点数据");
                        }
                        else
                        {
                            var value = pd.DataValue.ToConvertValueType(parameterInfo.ParameterType);
                            sb_invoke_login.AppendCode(3, $"global::{paramtTypeFullName} value{index} = (global::{paramtTypeFullName}){value}; // 获取当前节点的上一节点数据");

                        }
                    }
                    else if (parameterInfo.ParameterType == typeof(string))
                    {
                        var dataString = EscapeForCSharpString(pd.DataValue);
                        sb_invoke_login.AppendCode(3, $"global::{paramtTypeFullName} value{index} = \"{dataString}\"; // 获取当前节点的上一节点数据");
                    }
                    else
                    {
                        // 处理表达式
                    }

                }
                else
                {
                    #region 非显式设置的参数以正常方式获取
                    if (pd.ArgDataSourceType == ConnectionArgSourceType.GetPreviousNodeData)
                    {
                        var valueType = pd.IsParams ? $"global::{pd.DataType.GetFriendlyName()}" : $"global::{paramtTypeFullName}";
                        if (typeof(IFlowContext).IsAssignableFrom(pd.DataType))
                        {
                            sb_invoke_login.AppendCode(3, $"{valueType} value{index} = {flowContext}; // 使用流程上下文");
                        }
                        else
                        {
                            var previousNode = $"previousNode{index}";
                            sb_invoke_login.AppendCode(3, $"global::System.String {previousNode} = {flowContext}.GetPreviousNode(\"{singleScriptNode.Guid}\");"); // 获取运行时上一节点Guid
                            sb_invoke_login.AppendCode(3, $"{valueType} value{index} = {previousNode} == null ? default : ({valueType}){flowContext}.{nameof(IFlowContext.GetFlowData)}({previousNode}).Value; // 获取运行时上一节点的数据");
                        }

                        // var previousNode = $"previousNode{index}";
                        // var valueType = pd.IsParams ? $"global::{pd.DataType.GetFriendlyName()}" : $"global::{paramtTypeFullName}";
                        // sb_invoke_login.AppendCode(3, $"global::System.String {previousNode} = {flowContext}.GetPreviousNode(\"{singleScriptNode.Guid}\");"); // 获取运行时上一节点Guid
                        // sb_invoke_login.AppendCode(3, $"{valueType} value{index} = {previousNode} == null ? default : ({valueType}){flowContext}.{nameof(IFlowContext.GetFlowData)}({previousNode}).Value; // 获取运行时上一节点的数据");
                    }
                    else if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeData)
                    {
                        if (flowModelService.TryGetNodeModel(pd.ArgDataSourceNodeGuid, out var otherNode))
                        {
                            var valueType = pd.IsParams ? $"global::{pd.DataType.FullName}" : $"global::{paramtTypeFullName}";
                            var otherNodeReturnType = otherNode.MethodDetails.ReturnType;
                            if (otherNodeReturnType == typeof(object))
                            {
                                sb_invoke_login.AppendCode(3, $"{valueType} value{index} = ({valueType}){flowContext}.{nameof(IFlowContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
                            }
                            else if (pd.DataType.IsAssignableFrom(otherNodeReturnType))
                            {
                                sb_invoke_login.AppendCode(3, $"{valueType} value{index} = ({valueType}){flowContext}.{nameof(IFlowContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
                            }
                            else
                            {
                                // 获取的数据无法转换为目标方法入参类型
                                throw new Exception("获取的数据无法转换为目标方法入参类型");
                            }
                        }
                        else
                        {
                            // 指定了Guid，但项目中不存在对应的节点，需要抛出异常
                            throw new Exception("指定了Guid，但项目中不存在对应的节点");
                        }
                    }
                    else if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeDataOfInvoke)
                    {
                        if (flowModelService.TryGetNodeModel(pd.ArgDataSourceNodeGuid, out var otherNode)) // 获取指定节点
                        {
                            var otherNodeReturnType = otherNode.MethodDetails.ReturnType;
                            var valueType = pd.IsParams ? $"global::{pd.DataType.FullName}" : $"global::{otherNode.MethodDetails.ReturnType.FullName}";
                            if (otherNodeReturnType == typeof(object))
                            {
                                sb_invoke_login.AppendCode(3, $"{valueType} value{index} = ({valueType}){flowContext}.{nameof(IFlowContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
                            }
                            else if (pd.DataType.IsAssignableFrom(otherNodeReturnType))
                            {
                                if (typeof(Task).IsAssignableFrom(otherNode.MethodDetails.ReturnType))
                                {
                                    sb_invoke_login.Append("await ");
                                }
                                sb_invoke_login.AppendCode(3, $"{otherNode.ToNodeMethodName()}({flowContext}); // 需要立即调用指定方法");
                                sb_invoke_login.AppendCode(3, $"{valueType} value{index} = ({valueType}){flowContext}.{nameof(IFlowContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
                            }
                            else
                            {
                                // 获取的数据无法转换为目标方法入参类型
                                throw new Exception("获取的数据无法转换为目标方法入参类型");
                            }

                        }
                        else
                        {
                            // 指定了Guid，但项目中不存在对应的节点，需要抛出异常
                            throw new Exception("指定了Guid，但项目中不存在对应的节点");
                        }
                    }
                    #endregion

                }
            }

            if (scriptMethodInfo.ReturnType == typeof(void))
            {
                // 调用无返回值方法
                // 如果目标方法是静态的，则以“命名空间.类.方法”形式调用，否则以“实例.方法”形式调用
                var invokeFunctionContext = $"{scriptMethodInfo.ClassName}.{scriptMethodInfo.MethodName}";
                // 如果目标方法是异步的，则自动 await 进行等待
                invokeFunctionContext = md.IsAsync ? $"await {invokeFunctionContext}" : invokeFunctionContext;
                sb_invoke_login.AppendCode(3, $"global::{invokeFunctionContext}(", false);
                for (int index = 0; index < pds.Length; index++)
                {
                    sb_invoke_login.Append($"{(index == 0 ? "" : ",")}value{index}");
                }
                sb_invoke_login.AppendCode(0, $"); // 调用生成的C#代码");
            }
            else
            {
                // 调用有返回值方法
                // 如果目标方法是静态的，则以“命名空间.类.方法”形式调用，否则以“实例.方法”形式调用
                var invokeFunctionContext = $"{scriptMethodInfo.ClassName}.{scriptMethodInfo.MethodName}";
                // 如果目标方法是异步的，则自动 await 进行等待
                invokeFunctionContext = md.IsAsync ? $"await {invokeFunctionContext}" : invokeFunctionContext;
                sb_invoke_login.AppendCode(3, $"var result = {invokeFunctionContext}(", false);
                for (int index = 0; index < pds.Length; index++)
                {
                    sb_invoke_login.Append($"{(index == 0 ? "" : ",")}value{index}");
                }
                sb_invoke_login.AppendCode(0, $"); // 调用生成的C#代码");

                sb_invoke_login.AppendCode(3, $"{flowContext}.{nameof(IFlowContext.AddOrUpdate)}(\"{singleScriptNode.Guid}\", result);", false); // 更新数据
                //sb_invoke_login.AppendCode(3, $"return result;", false);
            }
            #endregion

            // global::{returnTypeFullName}

            var resultTypeName = singleScriptNode.MethodDetails.IsAsync ? "async Task" : "void";

            sb_main.AppendCode(2, $"[Description(\"脚本节点\")]");
            sb_main.AppendCode(2, $"private {resultTypeName} {singleScriptNode.ToNodeMethodName()}(global::{flowContextTypeName} {flowContext})");
            sb_main.AppendCode(2, $"{{");
            sb_main.AppendCode(0, sb_invoke_login.ToString());
            sb_main.AppendCode(2, $"}}"); // 方法结束
            sb_main.AppendLine(); // 方法结束




        }

        /// <summary>
        /// 生成[GlobalData]节点的方法调用
        /// </summary>
        /// <param name="sb_main"></param>
        /// <param name="globalDataNode"></param>
        /// <param name="flowContextTypeName"></param>
        /// <param name="flowContext"></param>
        private void CreateMethodCore_GlobalData(StringBuilder sb_main, SingleGlobalDataNode globalDataNode, string? flowContextTypeName, string flowContext)
        {
            if (!_globalDataInfos.TryGetValue(globalDataNode, out var globalDataInfo))
            {
                return;
            }

            var dataSourceNode = globalDataInfo.DataSourceNode;
            var returnType = dataSourceNode.MethodDetails.ReturnType;
            
            var keyName = globalDataInfo.KeyName;

            sb_main.AppendCode(2, $"[Description(\"全局数据\")]");
            sb_main.AppendCode(2, $"private void {globalDataNode.ToNodeMethodName()}(global::{flowContextTypeName} {flowContext})");
            sb_main.AppendCode(2, $"{{");

            if (typeof(Task).IsAssignableFrom(returnType))
            {
                sb_main.AppendCode(3, $"await {dataSourceNode.ToNodeMethodName()}({flowContext}); // 需要立即调用指定方法");
            }
            else
            {
                sb_main.AppendCode(3, $"{dataSourceNode.ToNodeMethodName()}({flowContext}); // 需要立即调用指定方法");
            }
            var returnTypeFullName = $"global::{returnType.GetFriendlyName()}";
            string data = nameof(data);
            sb_main.AppendCode(3, $"{returnTypeFullName} {data} = ({returnTypeFullName}){flowContext}.{nameof(IFlowContext.GetFlowData)}(\"{dataSourceNode.Guid}\").Value; // 获取指定节点的数据");
            sb_main.AppendCode(3, $"global::{typeof(SereinEnv).FullName}.{nameof(SereinEnv.AddOrUpdateFlowGlobalData)}(\"{keyName}\", {data}); // 设置全局数据");
            sb_main.AppendCode(3, $"{flowContext}.{nameof(IFlowContext.AddOrUpdate)}(\"{globalDataNode.Guid}\", {data});  // 更新全局数据节点的数据");
            sb_main.AppendCode(3, $"{flowContext}.{nameof(IFlowContext.AddOrUpdate)}(\"{dataSourceNode.Guid}\", {data}); // 更新全局数据节点数据来源节点的数据"); 
            sb_main.AppendCode(2, $"}}"); // 方法结束
            sb_main.AppendLine(); // 方法结束

        }



        #endregion

        #region 全局触发器与分支触发器生成

        /*private Dictionary<SingleFlipflopNode, SereinFlipflopMethodInfo> _flipflopNodeInfos = [];

        private class SereinFlipflopMethodInfo(bool isGlobal)
        {
            public bool IsGlobal { get; } = isGlobal;
        }
        private void GenerateFlipflop_InitSereinFlipflopMethodInfos(SingleFlipflopNode[])
        {
        }*/

        #endregion

        #region 全局节点的代码生成

        private Dictionary<SingleGlobalDataNode, SereinGlobalDataInfo> _globalDataInfos = [];
        private const string FlowGlobalData = nameof(FlowGlobalData);
        private class SereinGlobalDataInfo
        {
            /// <summary>
            /// 全局数据节点
            /// </summary>
            public required SingleGlobalDataNode Node { get; set; }

            /// <summary>
            /// 全局数据的来源节点
            /// </summary>
            public required IFlowNode DataSourceNode { get; set; }

            /// <summary>
            /// 全局数据的键名
            /// </summary>
            public string KeyName { get; set; } = string.Empty;
            /// <summary>
            /// 全局数据的类型
            /// </summary>
            public Type DataType { get; set; } = typeof(object);
        }


        private void GenerateGlobalData_InitSereinGlobalDataInfos(SingleGlobalDataNode[] globalDataNodes)
        {
            foreach(var node in globalDataNodes)
            {
                var keyName = node.KeyName;
                var dataNode = node.DataNode;
                if(dataNode is null)
                {
                    throw new Exception($"全局数据节点[{node}]没有指定数据来源节点");
                }
                var type = dataNode.MethodDetails.ReturnType;
                if (type is null || type == typeof(void))
                {
                    throw new Exception($"全局数据节点[{node}]无返回值");
                }
                _globalDataInfos[node] = new SereinGlobalDataInfo
                {
                    Node = node,
                    DataSourceNode = dataNode,
                    KeyName = keyName,
                    DataType = type,
                };
            }
        }

        /// <summary>
        /// 生成数据实体类
        /// </summary>
        /// <param name="sb"></param>
        private void GenerateGlobalData_ToClass(StringBuilder sb)
        {
            var infos = _globalDataInfos.Values.ToArray();
            sb.AppendCode(1, $"public sealed class {FlowGlobalData}");
            sb.AppendCode(1, $"{{");
            foreach (var info in infos)
            {
                var xmlDescription = $"{$"全局数据，来源于[{info.Node.MethodDetails?.MethodName}]{info.Node.Guid}".ToXmlComments(2)}";
                sb.AppendCode(2, xmlDescription);
                sb.AppendCode(2, $"public global::{info.DataType.FullName} {info.KeyName}  {{ get; set; }}");
            }
            sb.AppendLine();
            sb.AppendCode(1, $"}}");
        }


        #endregion

        #region 脚本节点的代码生成
        private Dictionary<SingleScriptNode, SereinScriptMethodInfo> _scriptMethodInfos = [];
        private void GenerateScript_InitSereinScriptMethodInfos(SingleScriptNode[] flowCallNodes)
        {
            _scriptMethodInfos.Clear();
            bool isError = false;   
            foreach(var node in flowCallNodes)
            {
                var methodName = node.ToNodeMethodName();
                var info = node.ToCsharpMethodInfo(methodName);
                if(info is null)
                {
                    isError = true;
                    SereinEnv.WriteLine(InfoType.WARN, $"脚本节点[{node.Guid}]无法生成代码信息");
                }
                else
                {
                    _scriptMethodInfos[node] = info;
                }

            }
            if (isError)
            {

            }
        }
        #endregion

        #region 流程接口节点的代码生成

        /// <summary>
        /// 流程接口节点与对应的流程方法信息
        /// </summary>

        private Dictionary<SingleFlowCallNode, FlowApiMethodInfo> _flowApiMethodInfos = [];

        /// <summary>
        /// 生成流程接口方法信息
        /// </summary>
        /// <param name="flowCallNodes"></param>
        private void GenerateFlowApi_InitFlowApiMethodInfos(SingleFlowCallNode[] flowCallNodes)
        {
            _flowApiMethodInfos.Clear(); 
            flowCallNodes = flowCallNodes.Where(node => !string.IsNullOrWhiteSpace(node.TargetNodeGuid)
                                                             && !flowModelService.ContainsCanvasModel(node.TargetNodeGuid))
                                         .ToArray(); // 筛选流程接口节点，只生成有效的

            foreach (var flowCallNode in flowCallNodes)
            {
                var info = flowCallNode.ToFlowApiMethodInfo();
                if (info is not null)
                {
                    _flowApiMethodInfos[flowCallNode] = info;
                }
            }
        }

        /// <summary>
        /// 生成流程接口模板类
        /// </summary>
        /// <param name="sb"></param>
        private void GenerateFlowApi_InterfaceAndImpleClass(StringBuilder sb)
        {
            /*
            最终生成的接口示例（示例不包含命名空间）：
            public interface IFlowApiInvoke
            {
                Task<NodeMethodReturnType> ApiInvoke(string name, int value);
                Task<NodeMethodReturnType> ApiInvoke(CancellationToken token, string name, int value);
                Task<NodeMethodReturnType> ApiInvoke(IDynamicContext context, CancellationToken token, string name, int value);
            }
            */

            sb.AppendCode(1, $"public interface IFlowApiInvoke");
            sb.AppendCode(1, $"{{");
            var infos = _flowApiMethodInfos.Values.ToArray();
            foreach (var info in infos)
            {
                var xmlDescription = $"{$"流程接口，{info.NodeModel.MethodDetails.MethodAnotherName}".ToXmlComments(2)}";
                sb.AppendCode(2, xmlDescription);
                sb.AppendCode(2, info.ToInterfaceMethodSignature(FlowApiMethodInfo.ParamType.Defute));
                sb.AppendCode(2, xmlDescription);
                sb.AppendCode(2, info.ToInterfaceMethodSignature(FlowApiMethodInfo.ParamType.HasToken));
                sb.AppendCode(2, xmlDescription);
                sb.AppendCode(2, info.ToInterfaceMethodSignature(FlowApiMethodInfo.ParamType.HasContextAndToken));
                /* sb.AppendCode(2, xmlDescription);
                sb.AppendCode(2, info.ToImpleMethodSignature(FlowApiMethodInfo.ParamType.Defute));
                sb.AppendCode(2, xmlDescription);
                sb.AppendCode(2, info.ToImpleMethodSignature(FlowApiMethodInfo.ParamType.HasToken));
                sb.AppendCode(2, xmlDescription);
                sb.AppendCode(2, info.ToImpleMethodSignature(FlowApiMethodInfo.ParamType.HasContextAndToken));*/
            }
            sb.AppendLine();
            sb.AppendCode(1, $"}}");

        }

        /// <summary>
        /// 生成流程接口参数
        /// </summary>
        /// <param name="sb"></param>
        private void GenerateFlowApi_ApiParamClass(StringBuilder sb)
        {
            var infos = _flowApiMethodInfos.Values.ToArray();
            foreach (var info in infos)
            {
                sb.AppendLine(info.ToParamterClassSignature());
            }
        }

        #endregion

        #region 辅助代码生成的方法

        /// <summary>
        /// 判断类型是否为静态类
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsStaticClass(Type type)
        {
            return type.IsAbstract && type.IsSealed && type.IsClass;
        }

        /// <summary>
        /// 节点ID映射
        /// </summary>
        private Dictionary<IFlowNode, int> nodeIdMap = new Dictionary<IFlowNode, int>();

        /// <summary>
        /// 获取节点ID
        /// </summary>
        /// <param name="flowNode"></param>
        /// <returns></returns>
        private int GetNodeId(IFlowNode flowNode)
        {
            if (nodeIdMap.ContainsKey(flowNode))
            {
                return nodeIdMap[flowNode];
            }
            else
            {
                lock (nodeIdMap)
                {
                    int id = nodeIdMap.Count + 1; // 从1开始计数
                    nodeIdMap[flowNode] = id;
                    return id;
                }
            }
        }

        
        private static string EscapeForCSharpString(string input)
        {
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\0", "\\0")
                .Replace("\a", "\\a")
                .Replace("\b", "\\b")
                .Replace("\f", "\\f")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")
                .Replace("\v", "\\v");
        }

        #endregion

    }



}
