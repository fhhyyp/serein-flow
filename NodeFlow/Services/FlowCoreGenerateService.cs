using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Env;
using Serein.NodeFlow.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Services
{
    /// <summary>
    /// 流程代码生成服务
    /// </summary>
    public class FlowCoreGenerateService
    {
        private readonly FlowModelService flowModelService;
        private readonly FlowLibraryService flowLibraryService;

        public FlowCoreGenerateService(FlowModelService flowModelService ,FlowLibraryService flowLibraryService )
        {
            this.flowModelService = flowModelService;
            this.flowLibraryService = flowLibraryService;
        }


        public string ToCsharpCoreFile()
        {
            StringBuilder stringBuilder = new StringBuilder();
            HashSet<Type> assemblyFlowClasss = new HashSet<Type>(); // 用于创建依赖注入项
            assemblyFlowClasss.Add(typeof(IFlowEnvironment)); // 调用树

            
            var flowNodes = flowModelService.GetAllNodeModel().ToArray();
            // 收集程序集信息
            foreach (var node in flowNodes)
            {
                var instanceType = node.MethodDetails.ActingInstanceType;
                if (instanceType is not null)
                {
                    assemblyFlowClasss.Add(instanceType);
                }

            }
            var flowCallNode = flowModelService.GetAllNodeModel().Where(n => n.ControlType == NodeControlType.FlowCall).Select(n => (SingleFlowCallNode)n).ToArray();
            GenerateFlowApi_InitFlowApiMethodInfos(flowCallNode);
            GenerateFlowApi_InterfaceAndImpleClass(stringBuilder); // 生成接口类
            GenerateFlowApi_ApiParamClass(stringBuilder); // 生成接口参数类
            string flowTemplateClassName = $"FlowTemplate"; // 类名
            string flowApiInterfaceName = $"IFlowApiInvoke"; // 类名
            stringBuilder.AppendCode(0, $"public class {flowTemplateClassName} : {flowApiInterfaceName}, global::{typeof(IFlowCallTree).FullName}");
            stringBuilder.AppendCode(0, $"{{");
            GenerateCtor(stringBuilder, flowTemplateClassName, assemblyFlowClasss); // 生成构造方法
            GenerateInitMethod(stringBuilder); // 生成初始化方法
            GenerateCallTree(stringBuilder, flowNodes); // 生成调用树
            GenerateNodeIndexLookup(stringBuilder, flowTemplateClassName, flowNodes); // 初始化节点缓存

            // 生成每个节点的方法
            foreach (var node in flowNodes)
            {
                GenerateMethod(stringBuilder, node); // 生成每个节点的方法
            }

            // 生成实现流程接口的实现方法
            var infos = flowApiMethodInfos.Values.ToArray();
            foreach (var info in infos)
            {
                stringBuilder.AppendCode(2, info.ToObjPoolSignature());
                stringBuilder.AppendCode(2, info.ToImpleMethodSignature(FlowApiMethodInfo.ParamType.Defute));
                stringBuilder.AppendCode(2, info.ToImpleMethodSignature(FlowApiMethodInfo.ParamType.HasToken));
                stringBuilder.AppendCode(2, info.ToImpleMethodSignature(FlowApiMethodInfo.ParamType.HasContextAndToken));
            }
            stringBuilder.AppendCode(0, $"}}");

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
            string? dynamicContextTypeName = typeof(IDynamicContext).FullName;
            string? flowContext = nameof(flowContext);

            if (flowNode.ControlType == NodeControlType.Action && flowNode is SingleActionNode actionNode)
            {
                CreateMethodCore_Action(sb_main, actionNode, dynamicContextTypeName, flowContext);
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
            else if (flowNode.ControlType == NodeControlType.FlowCall && flowNode is SingleFlowCallNode flowCallNode)
            {
                CreateMethodCore_FlowCall(sb_main, flowCallNode, dynamicContextTypeName, flowContext);
            }

            return;
            throw new Exception("无法为该节点生成调用逻辑");
        }

        /// <summary>
        /// 生成[Action]节点的方法调用
        /// </summary>
        /// <param name="sb_main"></param>
        /// <param name="actionNode"></param>
        /// <param name="dynamicContextTypeName"></param>
        /// <param name="flowContext"></param>
        /// <exception cref="Exception"></exception>
        private void CreateMethodCore_Action(StringBuilder sb_main, SingleActionNode actionNode, string? dynamicContextTypeName, string flowContext)
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
                        sb_invoke_login.AppendCode(3, $"global::System.String {previousNode} = {flowContext}.GetPreviousNode(\"{actionNode.Guid}\");"); // 获取运行时上一节点Guid
                        sb_invoke_login.AppendCode(3, $"{valueType} value{index} = {previousNode} == null ? default : ({valueType}){flowContext}.{nameof(IDynamicContext.GetFlowData)}({previousNode}).Value; // 获取运行时上一节点的数据");
                    }
                    else if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeData)
                    {
                        if (flowModelService.TryGetNodeModel(pd.ArgDataSourceNodeGuid, out var otherNode))
                        {
                            var valueType = pd.IsParams ? $"global::{pd.DataType.FullName}" : $"global::{paramtTypeFullName}";
                            var otherNodeReturnType = otherNode.MethodDetails.ReturnType;
                            if (otherNodeReturnType == typeof(object))
                            {
                                sb_invoke_login.AppendCode(3, $"{valueType} value{index} = ({valueType}){flowContext}.{nameof(IDynamicContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
                            }
                            else if (pd.DataType.IsAssignableFrom(otherNodeReturnType))
                            {
                                sb_invoke_login.AppendCode(3, $"{valueType} value{index} = ({valueType}){flowContext}.{nameof(IDynamicContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
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
                                sb_invoke_login.AppendCode(3, $"{valueType} value{index} = ({valueType}){flowContext}.{nameof(IDynamicContext.GetFlowData)}(\"{pd.ArgDataSourceNodeGuid}\").Value; // 获取指定节点的数据");
                            }
                            else if (pd.DataType.IsAssignableFrom(otherNodeReturnType))
                            {
                                sb_invoke_login.AppendCode(3, $"{valueType} value{index} = {otherNode.ToNodeMethodName()}({flowContext}); // 获取指定节点的数据");
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
                // 调用无返回值方法
                // 如果目标方法是静态的，则以“命名空间.类.方法”形式调用，否则以“实例.方法”形式调用
                var invokeFunctionContext = methodInfo.IsStatic ? $"global::{instanceType}.{methodInfo.Name}" : $"{instanceName}.{methodInfo.Name}";
                // 如果目标方法是异步的，则自动 await 进行等待
                invokeFunctionContext = md.IsAsync ? $"await {invokeFunctionContext}" : invokeFunctionContext;
                sb_invoke_login.AppendCode(3, $"global::{invokeFunctionContext}(", false);
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

                sb_invoke_login.AppendCode(3, $"{flowContext}.{nameof(IDynamicContext.AddOrUpdate)}(\"{actionNode.Guid}\", result);", false); // 更新数据
                //sb_invoke_login.AppendCode(3, $"return result;", false);
            }
            #endregion

            // global::{returnTypeFullName}

            var resultTypeName = actionNode.MethodDetails.IsAsync ? "async Task" : "void";

            sb_main.AppendCode(2, $"[Description(\"{instanceTypeFullName}.{methodInfo.Name}\")]");
            sb_main.AppendCode(2, $"private {resultTypeName} {actionNode.ToNodeMethodName()}(global::{dynamicContextTypeName} {flowContext})");
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
        /// <param name="dynamicContextTypeName"></param>
        /// <param name="flowContext"></param>
        /// <exception cref="Exception"></exception>
        private void CreateMethodCore_FlowCall(StringBuilder sb_main, SingleFlowCallNode flowCallNode, string? dynamicContextTypeName, string flowContext)
        {
            if (!flowApiMethodInfos.TryGetValue(flowCallNode, out var flowApiMethodInfo))
            {
                return;
            }
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
            var param = methodInfo.GetParameters();
            var md = flowCallNode.MethodDetails;
            var pds = flowCallNode.MethodDetails.ParameterDetailss;
            if (param is null) return;
            if (pds is null) return;

            for (int index = 0; index < pds.Length; index++)
            {
                ParameterDetails? pd = pds[index];
                ParameterInfo parameterInfo = param[index];
                var paramtTypeFullName = parameterInfo.ParameterType.FullName;
            }

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

                sb_invoke_login.AppendCode(4, $"{flowContext}.{nameof(IDynamicContext.AddOrUpdate)}(\"{flowCallNode.TargetNode.Guid}\", {resultName});"); // 更新数据
                sb_invoke_login.AppendCode(4, $"{flowApiMethodInfo.ObjPoolName}.Return({apiData});"); // 归还到对象池
                //sb_invoke_login.AppendCode(3, $"return result;", false);
            }
            sb_invoke_login.AppendCode(3, $"}}");
            sb_invoke_login.AppendCode(3, $"else");
            sb_invoke_login.AppendCode(3, $"{{");
            sb_invoke_login.AppendCode(4, $"throw new Exception(\"接口参数类型异常\");");
            sb_invoke_login.AppendCode(3, $"}}");




            #endregion

            // global::{returnTypeFullName}

            var resultTypeName = flowCallNode.MethodDetails.IsAsync ? "async Task" : "void";

            sb_main.AppendCode(2, $"[Description(\"{instanceTypeFullName}.{methodInfo.Name}\")]");
            sb_main.AppendCode(2, $"private {resultTypeName} {flowCallNode.ToNodeMethodName()}(global::{dynamicContextTypeName} {flowContext})");
            sb_main.AppendCode(2, $"{{");
            sb_main.AppendCode(0, sb_invoke_login.ToString());
            sb_main.AppendCode(2, $"}} "); // 方法结束

            sb_main.AppendLine(); // 方法结束

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
        private void GenerateCallTree(StringBuilder sb, IFlowNode[] flowNodes)
        {
            //  Get("0fa6985b-4b63-4499-80b2-76401669292d").AddChildNodeSucceed(Get("acdbe7ea-eb27-4a3e-9cc9-c48f642ee4f5"));
            sb.AppendCode(2, $"private void {nameof(GenerateCallTree)}()");
            sb.AppendCode(2, $"{{");

            foreach (var node in flowNodes)
            {
                var nodeMethod = node.ToNodeMethodName(); // 节点对应的方法名称
                if (node.ControlType == NodeControlType.Action || node.ControlType == NodeControlType.FlowCall)
                {
                    sb.AppendCode(3, $"Get(\"{node.Guid}\").SetAction({nodeMethod});");
                }
                else
                {
                    sb.AppendCode(3, $"// Get(\"{node.Guid}\").SetAction({nodeMethod}); // 暂未实现节点类型为[{node.ControlType}]的方法生成");

                }

            }

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


        private Dictionary<SingleFlowCallNode, FlowApiMethodInfo> flowApiMethodInfos = [];
        /// <summary>
        /// 生成流程接口方法信息
        /// </summary>
        /// <param name="flowCallNodes"></param>
        private void GenerateFlowApi_InitFlowApiMethodInfos(SingleFlowCallNode[] flowCallNodes)
        {

            flowCallNodes = flowCallNodes.Where(node => !string.IsNullOrWhiteSpace(node.TargetNodeGuid)
                                                             && !flowModelService.ContainsCanvasModel(node.TargetNodeGuid))
                                         .ToArray(); // 筛选流程接口节点，只生成有效的

            foreach (var flowCallNode in flowCallNodes)
            {
                var info = flowCallNode.ToFlowApiMethodInfo();
                if (info is not null)
                {
                    flowApiMethodInfos.TryAdd(flowCallNode, info);
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

            sb.AppendCode(2, $"public interface IFlowApiInvoke");
            sb.AppendCode(2, $"{{");
            var infos = flowApiMethodInfos.Values.ToArray();
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
            sb.AppendCode(2, $"}}");

        }

        /// <summary>
        /// 生成流程接口参数
        /// </summary>
        /// <param name="sb"></param>
        private void GenerateFlowApi_ApiParamClass(StringBuilder sb)
        {
            var infos = flowApiMethodInfos.Values.ToArray();
            foreach (var info in infos)
            {
                sb.AppendLine(info.ToParamterClassSignature());
            }
        }


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


        #endregion

    }














    internal class FlowApiMethodInfo
    {
        public FlowApiMethodInfo(SingleFlowCallNode singleFlowCallNode)
        {
            NodeModel = singleFlowCallNode;
        }
        public SingleFlowCallNode NodeModel { get; }  // 流程调用节点
        public string ApiMethodName { get; set; }
        public Type ReturnType { get; set; }
        public List<ParamInfo> ParamInfos { get; set; } = [];

        public string ParamTypeName => $"FlowApiInvoke_{ApiMethodName}";

        public class ParamInfo
        {
            public ParamInfo(Type type, string paramName)
            {
                Type = type;
                ParamName = paramName;
            }

            public Type Type { get; set; }
            public string ParamName { get; set; }
            public string Comments { get; set; }
        }


        public enum ParamType
        {
            Defute, // 仅使用方法参数
            HasToken, // 包含取消令牌和方法参数
            HasContextAndToken, // 包含上下文、取消令牌和方法参数
        }
        public bool IsVoid => ReturnType == typeof(void);
        public string ObjPoolName => $"_objPool{ParamTypeName.ToPascalCase()}";
        /// <summary>
        /// 生成所需的参数类的签名代码
        /// </summary>
        /// <returns></returns>
        public string ToParamterClassSignature()
        {
            StringBuilder sb = new StringBuilder();


            //sb.AppendCode(2, $"private static readonly global::Microsoft.Extensions.ObjectPool.DefaultObjectPool<{ParamTypeName}> {ObjPoolName} = ");
            //sb.AppendCode(2, $"    new global::Microsoft.Extensions.ObjectPool.DefaultObjectPool<{ParamTypeName}>(");
            //sb.AppendCode(2, $"         new global::Microsoft.Extensions.ObjectPool.DefaultPooledObjectPolicy<{ParamTypeName}>()); ");
            sb.AppendLine();
            var classXmlComments = $"流程接口[{ApiMethodName}]需要的参数类".ToXmlComments(2);
            sb.AppendCode(2, classXmlComments);
            sb.AppendCode(2, $"public class {ParamTypeName}");
            sb.AppendCode(2, $"{{");
            for (int index = 0; index < ParamInfos.Count; index++)
            {
                ParamInfo? info = ParamInfos[index];
                var argXmlComments = $"[{index}]流程接口参数{(string.IsNullOrWhiteSpace(info.Comments) ? string.Empty : $"，{info.Comments}。")}".ToXmlComments(2);
                sb.AppendCode(3, argXmlComments);
                sb.AppendCode(3, $"public global::{info.Type.FullName} {info.ParamName.ToPascalCase()} {{ get; set; }}");
            }
            sb.AppendCode(2, $"}}");
            return sb.ToString();
        }

        public string ToObjPoolSignature()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendCode(2, $"private static readonly global::Microsoft.Extensions.ObjectPool.DefaultObjectPool<{ParamTypeName}> {ObjPoolName} = ");
            sb.AppendCode(4, $"new global::Microsoft.Extensions.ObjectPool.DefaultObjectPool<{ParamTypeName}>(");
            sb.AppendCode(5, $"new global::Microsoft.Extensions.ObjectPool.DefaultPooledObjectPolicy<{ParamTypeName}>()); ");
            return sb.ToString();
        }

        /// <summary>
        /// 生成接口的签名方法
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string ToInterfaceMethodSignature(ParamType type)
        {
            var taskTypeFullName = $"global::System.Threading.Tasks.Task";
            var contextFullName = $"global::{typeof(IDynamicContext).FullName}";
            var tokenFullName = $"global::{typeof(CancellationToken).FullName}";
            var returnContext = IsVoid ? taskTypeFullName : $"{taskTypeFullName}<{ReturnType.FullName}>";
            if (type == ParamType.Defute)
            {
                var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{p.Type.FullName} {p.ParamName}"));
                return $"{returnContext} {ApiMethodName}({paramSignature});";
            }
            else if (type == ParamType.HasToken)
            {
                var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{p.Type.FullName} {p.ParamName}"));
                return $"{returnContext}  {ApiMethodName}({tokenFullName} token, {paramSignature});";
            }
            else if (type == ParamType.HasContextAndToken)
            {
                var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{p.Type.FullName} {p.ParamName}"));
                return $"{returnContext}  {ApiMethodName}({contextFullName} flowContext, {tokenFullName} token, {paramSignature});";
            }
            throw new Exception();
        }

        /// <summary>
        /// 生成实现方法的签名代码
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string ToImpleMethodSignature(ParamType type)
        {
            var taskTypeFullName = $"global::System.Threading.Tasks.Task";
            var contextApiFullName = $"global::{typeof(IDynamicContext).FullName}";
            var contextImpleFullName = $"global::{typeof(DynamicContext).FullName}";
            var tokenSourceFullName = $"global::{typeof(CancellationTokenSource).FullName}";
            var tokenFullName = $"global::{typeof(CancellationToken).FullName}";
            var flowContextPoolName = $"global::{typeof(LightweightFlowControl).FullName}";
            string flowEnvironment = nameof(flowEnvironment);
            string flowContext = nameof(flowContext);
            string token = nameof(token);

            var returnTypeContext = IsVoid ? taskTypeFullName : $"{taskTypeFullName}<{ReturnType.FullName}>";

            StringBuilder sb = new StringBuilder();
            if (IsVoid)
            {
                if (type == ParamType.Defute)
                {
                    var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{{{p.Type.FullName} {p.ParamName}"));
                    var invokeParamSignature = string.Join(", ", ParamInfos.Select(p => p.ParamName));
                    sb.AppendCode(2, $"public async {returnTypeContext} {ApiMethodName}({paramSignature})");
                    sb.AppendCode(2, $"{{");
                    sb.AppendCode(3,    $"{contextApiFullName} {flowContext} = {flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Allocate)}(); // 从对象池获取一个上下文");
                    sb.AppendCode(3,    $"{tokenSourceFullName} cts = new {tokenSourceFullName}(); // 创建取消令牌");
                    sb.AppendCode(3,    $"try");
                    sb.AppendCode(3,        $"{{");
                    sb.AppendCode(4,        $"await {ApiMethodName}({flowContext}, cts.Token, {invokeParamSignature}); // 调用目标方法");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"catch (Exception)");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"throw;");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"finally");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"{flowContext}.{nameof(IDynamicContext.Reset)}(); ");
                    sb.AppendCode(4,        $"cts.{nameof(CancellationTokenSource.Dispose)}(); ");
                    sb.AppendCode(4,        $"{flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Free)}({flowContext}); // 释放上下文");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(2, $"}}");
                    return sb.ToString();
                }
                else if (type == ParamType.HasToken)
                {
                    var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{{{p.Type.FullName} {p.ParamName}"));
                    var invokeParamSignature = string.Join(", ", ParamInfos.Select(p => p.ParamName));
                    sb.AppendCode(2, $"public async {returnTypeContext} {ApiMethodName}({tokenFullName} {token}, {paramSignature})");
                    sb.AppendCode(2, $"{{");
                    sb.AppendCode(3,    $"{contextApiFullName} {flowContext} = {flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Allocate)}(); // 从对象池获取一个上下文");
                    sb.AppendCode(3,    $"try");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"await {ApiMethodName}({flowContext}, {token}, {invokeParamSignature}); // 调用目标方法");
                    sb.AppendCode(4,        $"{flowContext}.{nameof(IDynamicContext.Reset)}(); ");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"catch (Exception)");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"throw;");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"finally");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"{flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Free)}({flowContext}); // 释放上下文");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(2, $"}}");
                    return sb.ToString();
                }
                else if (type == ParamType.HasContextAndToken)
                {
                    var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{{{p.Type.FullName} {p.ParamName}"));
                    var invokeParamSignature = string.Join(", ", ParamInfos.Select(p => p.ParamName));


                    sb.AppendCode(2, $"public async {returnTypeContext} {ApiMethodName}({contextApiFullName} {flowContext}, {tokenFullName} token, {paramSignature})");
                    sb.AppendCode(2, $"{{");
                    sb.AppendCode(3,    $"token.ThrowIfCancellationRequested(); // 检查任务是否取消");
                    sb.AppendCode(3,    $"try");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(3,        $"global::{ParamTypeName} data = {ObjPoolName}.Get(); // 从对象池获取一个对象");
                    for (int index = 0; index < ParamInfos.Count; index++)
                    {
                        ParamInfo? info = ParamInfos[index];
                        sb.AppendCode(4,    $"data.{info.ParamName.ToPascalCase()} = {info.ParamName}; // [{index}] {info.Comments}");
                    }
                    sb.AppendCode(3,        $"{flowContext}.{nameof(IDynamicContext.AddOrUpdate)}(\"{ApiMethodName}\", data);");
                    sb.AppendCode(3,        $"{flowContext}.{nameof(IDynamicContext.SetPreviousNode)}(\"{NodeModel.TargetNode.Guid}\", \"{ApiMethodName}\");");
                    sb.AppendCode(3,        $"global::{typeof(CallNode).FullName} node = Get(\"{NodeModel.Guid}\");");
                    sb.AppendCode(3,        $"await node.{nameof(CallNode.StartFlowAsync)}({flowContext}, {token}); // 调用目标方法");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"catch (Exception)");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"throw;");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"finally");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"{flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Free)}({flowContext}); // 释放上下文");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(2, $"}}");
                    return sb.ToString();
                }
            }
            else
            {
                string flowResult = nameof(flowResult);
                if (type == ParamType.Defute)
                {
                    //sb.AppendCode(3, $"{contextApiFullName} {flowContext} = new {contextImpleFullName}({flowEnvironment}); // 创建上下文");

                    var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{p.Type.FullName} {p.ParamName}"));
                    var invokeParamSignature = string.Join(", ", ParamInfos.Select(p => p.ParamName));
                    sb.AppendCode(2, $"public async {returnTypeContext} {ApiMethodName}({paramSignature})");
                    sb.AppendCode(2, $"{{");
                    sb.AppendCode(3,    $"{contextApiFullName} {flowContext} = {flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Allocate)}(); // 从对象池获取一个上下文");
                    sb.AppendCode(3,    $"{tokenSourceFullName} cts = new {tokenSourceFullName}(); // 创建取消令牌");
                    sb.AppendCode(3,    $"try");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,       $"{ReturnType.FullName} {flowResult} =  await {ApiMethodName}({flowContext}, cts.{nameof(CancellationTokenSource.Token)}, {invokeParamSignature}); // 调用目标方法");
                    sb.AppendCode(4,       $"return {flowResult};");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"catch (Exception)");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,       $"throw;");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"finally");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,       $"{flowContext}.{nameof(IDynamicContext.Reset)}(); ");
                    sb.AppendCode(4,       $"cts.{nameof(CancellationTokenSource.Dispose)}(); ");
                    sb.AppendCode(4,       $"{flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Free)}({flowContext}); // 释放上下文");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(2, $"}}");
                    return sb.ToString();
                }
                else if (type == ParamType.HasToken)
                {
                    var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{p.Type.FullName} {p.ParamName}"));
                    var invokeParamSignature = string.Join(", ", ParamInfos.Select(p => p.ParamName));
                    sb.AppendCode(2, $"public async {returnTypeContext} {ApiMethodName}({tokenFullName} {token}, {paramSignature})");
                    sb.AppendCode(2, $"{{");
                    sb.AppendCode(3,    $"{contextApiFullName} {flowContext} = {flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Allocate)}(); // 从对象池获取一个上下文");
                    sb.AppendCode(3,    $"try");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,       $"{ReturnType.FullName} {flowResult} = await {ApiMethodName}({flowContext}, {token}, {invokeParamSignature}); // 调用目标方法");
                    sb.AppendCode(4,       $"return {flowResult};");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"catch (Exception)");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,       $"throw;");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"finally");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,       $"{flowContext}.{nameof(IDynamicContext.Reset)}(); ");
                    sb.AppendCode(4,       $"{flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Free)}({flowContext}); // 释放上下文");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(2, $"}}");
                    return sb.ToString();
                }
                else if (type == ParamType.HasContextAndToken)
                {
                    var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{p.Type.FullName} {p.ParamName}"));
                    var invokeParamSignature = string.Join(", ", ParamInfos.Select(p => p.ParamName));
                    sb.AppendCode(2, $"public async {returnTypeContext} {ApiMethodName}({contextApiFullName} {flowContext}, {tokenFullName} token, {paramSignature})");
                    sb.AppendCode(2, $"{{");
                    sb.AppendCode(3,    $"token.ThrowIfCancellationRequested(); // 检查任务是否取消");
                    sb.AppendCode(3,    $"global::{ParamTypeName} data = {ObjPoolName}.Get(); // 从对象池获取一个对象");
                    for (int index = 0; index < ParamInfos.Count; index++)
                    {
                        ParamInfo? info = ParamInfos[index];
                        sb.AppendCode(4, $"data.{info.ParamName.ToPascalCase()} = {info.ParamName}; // [{index}] {info.Comments}"); // 进行赋值
                    }
                    sb.AppendCode(3,    $"{flowContext}.{nameof(IDynamicContext.AddOrUpdate)}(\"{ApiMethodName}\", data);");
                    sb.AppendCode(3,    $"{flowContext}.{nameof(IDynamicContext.SetPreviousNode)}(\"{NodeModel.Guid}\", \"{ApiMethodName}\");");
                    sb.AppendCode(3,    $"global::{typeof(CallNode).FullName} node = Get(\"{NodeModel.Guid}\");");
                    sb.AppendCode(3,    $"global::{typeof(FlowResult).FullName} {flowResult} = await node.{nameof(CallNode.StartFlowAsync)}({flowContext}, {token}); // 调用目标方法");
                    sb.AppendCode(3,    $"if ({flowResult}.{nameof(FlowResult.Value)} is global::{ReturnType.FullName} result)");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"return result;");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"else");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"throw new ArgumentNullException($\"类型转换失败，{{(flowResult.Value is null ? \"返回数据为 null\" : $\"返回数据与需求类型不匹配，当前返回类型为[{{flowResult.Value.GetType().FullName}}。\")}}\");");
                    sb.AppendCode(3,    $"}}");
                    //sb.AppendCode(3,    $"return {flowResult};");
                    sb.AppendCode(2, $"}}");
                    return sb.ToString();
                    // throw new ArgumentNullException($"类型转换失败，{(flowResult.Value is null ? "返回数据为 null" : $"返回数据与需求类型不匹配，当前返回类型为[{flowResult.Value.GetType().FullName}。")}");
                }
            }

            throw new Exception();

        }
    }

    internal static class CoreGenerateExtension
    {
        /// <summary>
        /// 生成流程接口信息描述
        /// </summary>
        /// <param name="flowCallNode"></param>
        /// <returns></returns>
        public static FlowApiMethodInfo? ToFlowApiMethodInfo(this SingleFlowCallNode flowCallNode)
        {
            var targetNode = flowCallNode.TargetNode;
            if (targetNode.ControlType is not (NodeControlType.Action or NodeControlType.Script)) return null;
            if (flowCallNode.MethodDetails is null) return null;
            if (string.IsNullOrWhiteSpace(flowCallNode.ApiGlobalName)) return null;


            FlowApiMethodInfo flowApiMethodInfo = new FlowApiMethodInfo(flowCallNode);
            flowApiMethodInfo.ReturnType = targetNode.ControlType == NodeControlType.Script ? typeof(object)
                                                                                            : flowCallNode.MethodDetails.ReturnType;

            flowApiMethodInfo.ApiMethodName = flowCallNode.ApiGlobalName;

            List<FlowApiMethodInfo.ParamInfo> list = [];

            int index = 0;
            foreach (var pd in flowCallNode.MethodDetails.ParameterDetailss)
            {
                if (pd.DataType is null || string.IsNullOrWhiteSpace(pd.Name))
                {
                    return null;
                }
                if (pd.IsParams)
                {
                    list.Add(new FlowApiMethodInfo.ParamInfo(pd.DataType, $"{pd.Name}{index++}"));
                }
                else
                {
                    list.Add(new FlowApiMethodInfo.ParamInfo(pd.DataType, pd.Name));
                }
            }

            flowApiMethodInfo.ParamInfos = list;
            return flowApiMethodInfo;
        }



        /// <summary>
        /// 生成方法名称
        /// </summary>
        /// <param name="flowNode"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToNodeMethodName(this IFlowNode flowNode)
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


        /// <summary>
        /// 生成完全的xml注释
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string ToXmlComments(this string context, int retractCount = 0)
        {
            StringBuilder sb = new StringBuilder();
            var startLine = "/// <summary>";
            var endLine = "/// </summary>";
            sb.AppendLine(startLine);
            var rows = context.Split(Environment.NewLine);
            string retract = new string(' ', retractCount * 4);
            foreach (var row in rows)
            {
                // 处理转义
                var value = row.Replace("<", "&lt")
                               .Replace(">", "&gt");
                sb.AppendLine($"{retract}/// <para>{value}</para>");
            }
            sb.AppendLine(endLine);
            return sb.ToString();
        }

        /// <summary>
        /// 生成类型的驼峰命名法名称（首字母小写）
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToCamelCase(this Type type)
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

        /// <summary>
        /// 生成类型的大驼峰命名法名称（PascalCase）
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToPascalCase(this Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            string typeName = type.Name;

            if (string.IsNullOrEmpty(typeName))
            {
                return string.Empty;
            }

            // 去掉泛型标记（如 `1）
            int indexOfBacktick = typeName.IndexOf('`');
            if (indexOfBacktick > 0)
            {
                typeName = typeName.Substring(0, indexOfBacktick);
            }

            // 如果是接口以 I 开头，且后面是大写，去掉前缀 I
            if (type.IsInterface && typeName.Length > 1 && typeName[0] == 'I' && char.IsUpper(typeName[1]))
            {
                typeName = typeName.Substring(1);
            }

            // 首字母转为大写（如果有需要）
            if (typeName.Length > 0)
            {
                return char.ToUpperInvariant(typeName[0]) + typeName.Substring(1);
            }

            return typeName;
        }

        /// <summary>
        /// 将字符串首字母大写（PascalCase）
        /// </summary>
        /// <param name="text">原始文本</param>
        /// <returns>首字母大写的文本</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToPascalCase(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (char.IsUpper(text[0]))
            {
                return text; // 已是大写
            }

            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }



}
