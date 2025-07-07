using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.NodeFlow.Services
{
    /// <summary>
    /// 流程模型服务
    /// </summary>
    public class FlowModelService
    {
        private readonly IFlowEnvironment environment;
        private readonly FlowLibraryService flowLibraryService;

        public FlowModelService(IFlowEnvironment environment, FlowLibraryService flowLibraryService )
        {
            this.environment = environment;
            this.flowLibraryService = flowLibraryService;
        }

        /// <summary>
        /// 环境加载的节点集合
        /// Node Guid - Node Model
        /// </summary>
        private Dictionary<string, IFlowNode> NodeModels { get; } = [];

        /// <summary>
        /// 运行环境加载的画布集合
        /// </summary>
        private Dictionary<string, FlowCanvasDetails> FlowCanvass { get; } = [];

        /// <summary>
        /// 存放触发器节点（运行时全部调用）
        /// </summary>
        private List<SingleFlipflopNode> FlipflopNodes { get; } = [];

        public IFlowNode? GetNodeModel(string guid)
        {
            NodeModels.TryGetValue(guid, out var nodeModel);
            return nodeModel;
        }
        
        public FlowCanvasDetails? GetCanvasModel(string guid)
        {
            FlowCanvass.TryGetValue(guid, out var nodeModel);
            return nodeModel;
        }

        public bool TryGetNodeModel(string guid,out IFlowNode flowNode)
        {
            return NodeModels.TryGetValue(guid, out flowNode!);
        }

        public bool TryGetCanvasModel(string guid,out FlowCanvasDetails flowCanvas)
        {
            return FlowCanvass.TryGetValue(guid, out flowCanvas!);;
        }


        public bool ContainsNodeModel(string guid)
        {
            return NodeModels.ContainsKey(guid);
        }
        
        public bool ContainsCanvasModel(string guid)
        {
            return FlowCanvass.ContainsKey(guid);
        }

        public bool AddNodeModel(IFlowNode flowNode)
        {
            ArgumentNullException.ThrowIfNull(flowNode);
            ArgumentNullException.ThrowIfNull(flowNode.Guid);
            return NodeModels.TryAdd(flowNode.Guid, flowNode);
        }
        public bool AddCanvasModel(FlowCanvasDetails flowCanvasDetails)
        {
            ArgumentNullException.ThrowIfNull(flowCanvasDetails);
            ArgumentNullException.ThrowIfNull(flowCanvasDetails.Guid);
            return FlowCanvass.TryAdd(flowCanvasDetails.Guid, flowCanvasDetails);
        }
        public bool RemoveNodeModel(IFlowNode flowNode)
        {
            ArgumentNullException.ThrowIfNull(flowNode.Guid);
            return NodeModels.Remove(flowNode.Guid);
        }
        public bool RemoveCanvasModel(FlowCanvasDetails flowCanvasDetails)
        {
            ArgumentNullException.ThrowIfNull(flowCanvasDetails.Guid);
            return FlowCanvass.Remove(flowCanvasDetails.Guid);
        }

        public List<IFlowNode> GetAllNodeModel() => [.. NodeModels.Values];
        public List<IFlowNode> GetAllNodeModel(string canvasGuid) =>
            NodeModels.Values.Where(x => x.CanvasDetails.Guid == canvasGuid).ToList();
        public List<FlowCanvasDetails> GetAllCanvasModel() => [.. FlowCanvass.Values];



        public bool IsExsitCanvas()
        {
            return FlowCanvass.Count > 0;
        }
        public bool IsExsitNodeOnCanvas(string canvasGuid)
        {
            if (!FlowCanvass.TryGetValue(canvasGuid, out var flowCanvasDetails))
            {
                return false;
            }
            return flowCanvasDetails.Nodes.Count > 0;
        }

        #region 代码生成

        public string ToCsharpCoreFile()
        {
            #region 生成类和方法
#if false
            HashSet<Type> assemblyFlowClasss = new HashSet<Type>(); // 用于创建依赖注入项
            assemblyFlowClasss.Add(typeof(IFlowCallTree)); // 调用树
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var canvas in FlowCanvass.Values)
            {
                if (canvas.StartNode is null)
                {
                    continue;
                }
                int flowTemplateId = canvas_index++;
                string flowTemplateClassName = $"FlowTemplate_{canvas.Guid.Replace("-", "")}";

                HashSet<Type> flowClasss = new HashSet<Type>();
                flowClasss.Add(typeof(IFlowCallTree)); // 调用树
                // 收集程序集信息
                foreach (var node in canvas.Nodes)
                {
                    var instanceType = node.MethodDetails.ActingInstanceType;
                    if (instanceType is not null)
                    {
                        flowClasss.Add(instanceType);
                        assemblyFlowClasss.Add(instanceType);
                    }
                }

                stringBuilder.AppendCode(0, $"public class {flowTemplateClassName}");
                stringBuilder.AppendCode(0, $"{{");

                // 构造函数及依赖注入字段
                GenerateCtor(stringBuilder, flowTemplateClassName, flowClasss);
                //GenerateNodeIndexLookup(stringBuilder, flowTemplateClassName, );
                GenerateInitMethod(stringBuilder);
                GenerateCallTree(stringBuilder, canvas);

                // 节点生成方法信息
                foreach (var node in canvas.Nodes)
                {
                    GenerateMethod(stringBuilder, node);
                }
                stringBuilder.AppendCode(0, $"}}");

            } 
#else
            StringBuilder stringBuilder = new StringBuilder();
            HashSet<Type> assemblyFlowClasss = new HashSet<Type>(); // 用于创建依赖注入项
            assemblyFlowClasss.Add(typeof(IFlowCallTree)); // 调用树
            var flowNodes = NodeModels.Values.ToArray();
            // 收集程序集信息
            foreach (var node in flowNodes)
            {
                var instanceType = node.MethodDetails.ActingInstanceType;
                if (instanceType is not null)
                {
                    assemblyFlowClasss.Add(instanceType);
                }

            }

            string flowTemplateClassName = $"FlowTemplate"; // 类名
            stringBuilder.AppendCode(0, $"public class {flowTemplateClassName} : global::{typeof(IFlowCallTree).FullName}");
            stringBuilder.AppendCode(0, $"{{");
            GenerateCtor(stringBuilder, flowTemplateClassName, assemblyFlowClasss); // 生成构造方法
            GenerateInitMethod(stringBuilder); // 生成初始化方法
            GenerateCallTree(stringBuilder, flowNodes); // 生成调用树
            GenerateNodeIndexLookup(stringBuilder, flowTemplateClassName, flowNodes); // 初始化节点缓存
            foreach (var node in flowNodes)
            {
                GenerateMethod(stringBuilder, node); // 生成每个节点的方法
            }
            stringBuilder.AppendCode(0, $"}}");
#endif
            #endregion


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
                var ctor_parms_name = GetCamelCase(type);
                sb.AppendCode(2, $"private readonly global::{type.FullName} {ctor_parms_name};");
            }

            sb.AppendLine();

            sb.AppendCode(2, $"public {className}(", false);
            for (int index = 0; index < instanceTypes.Length; index++)
            {
                var type = instanceTypes[index];
                var ctor_parms_name = GetCamelCase(type);
                sb.Append($"global::{type.FullName} {ctor_parms_name}{(index < instanceTypes.Length - 1 ? "," : "")}");
            }
            sb.AppendCode(0, $")");
            sb.AppendCode(2, $"{{");
            for (int index = 0; index < instanceTypes.Length; index++)
            {
                var type = instanceTypes[index];
                var ctor_parms_name = GetCamelCase(type);
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
            string? dynamicContextTypeName = typeof(IDynamicContext).FullName;
            string? flowContext = nameof(flowContext);

            if (flowNode.ControlType == NodeControlType.Action)
            {
                #region 生成 Action 节点类型的调用过程
                if (!flowLibraryService.TryGetMethodInfo(flowNode.MethodDetails.AssemblyName,
                                                                    flowNode.MethodDetails.MethodName,
                                                                    out var methodInfo) || methodInfo is null)
                {
                    return;
                }

                var isRootNode = flowNode.IsRoot();

                var instanceType = flowNode.MethodDetails.ActingInstanceType;
                var returnType = methodInfo.ReturnType;

                var instanceName = GetCamelCase(instanceType);// $"instance_{instanceType.Name}";

                var instanceTypeFullName = instanceType.FullName;
                var returnTypeFullName = returnType == typeof(void) ? "void" : returnType.FullName;

                #region 方法内部逻辑
                StringBuilder sb_invoke_login = new StringBuilder();
                if (flowNode.MethodDetails is null) return;
                var param = methodInfo.GetParameters();
                var md = flowNode.MethodDetails;
                var pds = flowNode.MethodDetails.ParameterDetailss;
                if (param is null) return;
                if (pds is null) return;

                bool isGetPreviousNode = false;
                for (int index = 0; index < pds.Length; index++)
                {
                    ParameterDetails? pd = pds[index];
                    ParameterInfo parameterInfo = param[index];
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
                                var value = pd.DataValue.ToConvert(parameterInfo.ParameterType);
                                sb_invoke_login.AppendCode(3, $"global::{paramtTypeFullName} value{index} = (global::{paramtTypeFullName}){value}; // 获取当前节点的上一节点数据");

                            }
                        }
                        else if (parameterInfo.ParameterType == typeof(string))
                        {
                            sb_invoke_login.AppendCode(3, $"global::{paramtTypeFullName} value{index} = \"{pd.DataValue}\"; // 获取当前节点的上一节点数据");
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
                            var previousNode = $"previousNode{index}";
                            var valueType = pd.IsParams ? $"global::{pd.DataType.FullName}" : $"global::{paramtTypeFullName}";
                            sb_invoke_login.AppendCode(3, $"global::System.String {previousNode} = {flowContext}.GetPreviousNode(\"{flowNode.Guid}\");"); // 获取运行时上一节点Guid
                            sb_invoke_login.AppendCode(3, $"{valueType} value{index} = {previousNode} == null ? default : ({valueType}){flowContext}.{nameof(IDynamicContext.GetFlowData)}({previousNode}).Value; // 获取运行时上一节点的数据");
                        }
                        else if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeData)
                        {
                            if (this.TryGetNodeModel(pd.ArgDataSourceNodeGuid, out var otherNode))
                            {
                                var valueType = pd.IsParams ? $"global::{pd.DataType.FullName}" : $"global::{paramtTypeFullName}";
                                var otherNodeReturnType = otherNode.MethodDetails.ReturnType;
                                if (otherNodeReturnType == typeof(object))
                                {
                                    sb_invoke_login.AppendCode(3, $"{valueType} value{index} = ({valueType}){flowContext}.{nameof(IDynamicContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
                                }
                                else if (pd.DataType.IsAssignableFrom(otherNodeReturnType))
                                {
                                    sb_invoke_login.AppendCode(3, $"{valueType} value{index} = {flowContext}.{nameof(IDynamicContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
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
                            if (this.TryGetNodeModel(pd.ArgDataSourceNodeGuid, out var otherNode)) // 获取指定节点
                            {
                                var otherNodeReturnType = otherNode.MethodDetails.ReturnType;
                                var valueType = pd.IsParams ? $"global::{pd.DataType.FullName}" : $"global::{otherNode.MethodDetails.ReturnType.FullName}";
                                if (otherNodeReturnType == typeof(object))
                                {
                                    sb_invoke_login.AppendCode(3, $"{valueType} value{index} = ({valueType}){flowContext}.{nameof(IDynamicContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
                                }
                                else if (pd.DataType.IsAssignableFrom(otherNodeReturnType))
                                {
                                    sb_invoke_login.AppendCode(3, $"{valueType} value{index} = {GetNodeMethodName(otherNode)}({flowContext}); // 获取指定节点的数据");
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



                if (methodInfo.ReturnType == typeof(void))
                {
                    if (methodInfo.IsStatic)
                    {
                        sb_invoke_login.AppendCode(3, $"global::{instanceType}.{methodInfo.Name}(", false);
                        for (int index = 0; index < pds.Length; index++)
                        {
                            sb_invoke_login.Append($"{(index == 0 ? "" : ",")}value{index}");
                        }
                        sb_invoke_login.AppendCode(0, $"); // 调用方法 {md.MethodAnotherName}");
                    }
                    else
                    {
                        sb_invoke_login.AppendCode(3, $"{instanceName}.{methodInfo.Name}(", false);
                        for (int index = 0; index < pds.Length; index++)
                        {
                            sb_invoke_login.Append($"{(index == 0 ? "" : ",")}value{index}");
                        }
                        sb_invoke_login.AppendCode(0, $"); // 调用方法 {md.MethodAnotherName}");
                    }
                }
                else
                {
                    if (methodInfo.IsStatic)
                    {
                        sb_invoke_login.AppendCode(3, $"var result = global::{instanceType}.{methodInfo.Name}(", false);
                        for (int index = 0; index < pds.Length; index++)
                        {
                            sb_invoke_login.Append($"{(index == 0 ? "" : ",")}value{index}");
                        }
                        sb_invoke_login.AppendCode(0, $"); // 调用方法 {md.MethodAnotherName}");
                    }
                    else
                    {
                        sb_invoke_login.AppendCode(3, $"var result = {instanceName}.{methodInfo.Name}(", false);
                        for (int index = 0; index < pds.Length; index++)
                        {
                            sb_invoke_login.Append($"{(index == 0 ? "" : ",")}value{index}");
                        }
                        sb_invoke_login.AppendCode(0, $"); // 调用方法 {md.MethodAnotherName}");
                    }

                    sb_invoke_login.AppendCode(3, $"{flowContext}.{nameof(IDynamicContext.AddOrUpdate)}(\"{flowNode.Guid}\", result);", false);
                    //sb_invoke_login.AppendCode(3, $"return result;", false);
                }
                #endregion

                // global::{returnTypeFullName}

                sb_main.AppendCode(2, $"[Description(\"{instanceTypeFullName}.{methodInfo.Name}\")]");
                sb_main.AppendCode(2, $"public void {GetNodeMethodName(flowNode)}(global::{dynamicContextTypeName} {flowContext})");
                sb_main.AppendCode(2, $"{{");
                sb_main.AppendCode(0, sb_invoke_login.ToString());
                sb_main.AppendCode(2, $"}}"); // 方法结束
                sb_main.AppendLine(); // 方法结束




                #endregion
            }
            else if (flowNode.ControlType == NodeControlType.Flipflop)
            {
            }
            else if (flowNode.ControlType == NodeControlType.Script)
            {
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

            return;
            throw new Exception("无法为该节点生成调用逻辑");
        }

        private void GenerateInitMethod(StringBuilder sb)
        {
            sb.AppendCode(2, $"public void Init()");
            sb.AppendCode(2, $"{{");
            sb.AppendCode(3, $"{nameof(GenerateCallTree)}(); // 初始化调用树"); // 初始化调用树
            sb.AppendCode(2, $"}}");
        }

        private void GenerateCallTree(StringBuilder sb, IFlowNode[] flowNodes)
        {
            //  Get("0fa6985b-4b63-4499-80b2-76401669292d").AddChildNodeSucceed(Get("acdbe7ea-eb27-4a3e-9cc9-c48f642ee4f5"));

            sb.AppendCode(2, $"private void {nameof(GenerateCallTree)}()");
            sb.AppendCode(2, $"{{");

            foreach (var node in flowNodes)
            {
                var nodeMethod = GetNodeMethodName(node); // 节点对应的方法名称
                sb.AppendCode(3, $"Get(\"{node.Guid}\").SetAction({nodeMethod});");
            }

            foreach (var node in flowNodes)
            {
                var nodeMethod = GetNodeMethodName(node); // 节点对应的方法名称
                var cts = NodeStaticConfig.ConnectionTypes;
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
            sb.AppendCode(2, $" [MethodImpl(MethodImplOptions.AggressiveInlining)]"); // 内联优化
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

        /// <summary>
        /// 生成方法名称
        /// </summary>
        /// <param name="flowNode"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetNodeMethodName(IFlowNode flowNode)
        {
            /*if (!flowLibraryService.TryGetMethodInfo(flowNode.MethodDetails.AssemblyName,
                                                           flowNode.MethodDetails.MethodName,
                                                           out var methodInfo))
            {
                throw new Exception();
            }*/
            var guid = flowNode.Guid;
            var tmp = guid.Replace("-", "");
            var methodName = $"FlowMethod_{tmp}";
            return methodName;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsStaticClass(Type type)
        {
            return type.IsAbstract && type.IsSealed && type.IsClass;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetCamelCase(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            // 获取类型名称（不包括命名空间）
            string typeName = type.Name;

            if (string.IsNullOrEmpty(typeName))
            {
                return string.Empty;
            }

            // 处理泛型类型（去掉后面的`N）
            int indexOfBacktick = typeName.IndexOf('`');
            if (indexOfBacktick > 0)
            {
                typeName = typeName.Substring(0, indexOfBacktick);
            }

            // 如果是接口且以"I"开头，去掉第一个字母
            if (type.IsInterface && typeName.Length > 1 && typeName[0] == 'I' && char.IsUpper(typeName[1]))
            {
                typeName = typeName.Substring(1);
            }

            // 转换为驼峰命名法：首字母小写，其余不变
            if (typeName.Length > 0)
            {
                return char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
            }

            return typeName;
        }

        private Dictionary<IFlowNode, int> nodeIdMap = new Dictionary<IFlowNode, int>();
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


        #endregion
    }
}


/*        /// <summary>
        /// 生成方法名称
        /// </summary>
        /// <param name="flowNode"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private string GetNodeMethodName(IFlowNode flowNode)
        {
            return $"FlowMethod_{flowNode.Guid.Remove('-')}";

            if (flowNode.ControlType == NodeControlType.Action)
            {
                if (!flowLibraryService.TryGetMethodInfo(flowNode.MethodDetails.AssemblyName,
                                                           flowNode.MethodDetails.MethodName,
                                                           out var methodInfo))
                {
                    throw new Exception();
                }
                return $"FlowMethod_{nameof(NodeControlType.Action)}_{methodInfo.Name}";
            }
            else if (flowNode.ControlType == NodeControlType.Flipflop)
            {
                if (!flowLibraryService.TryGetMethodInfo(flowNode.MethodDetails.AssemblyName,
                                                           flowNode.MethodDetails.MethodName,
                                                           out var methodInfo))
                {
                    throw new Exception();
                }
                return $"FlowMethod_{nameof(NodeControlType.Flipflop)}_{methodInfo.Name}";

            }
            else if (flowNode.ControlType == NodeControlType.Script)
            {
                return $"FlowMethod_{flowNode.Guid.Remove('-')}";
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
            else 
            {
                throw new Exception("无法为该节点生成方法名称");
            }
        }*/