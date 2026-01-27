using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Services;
using Serein.Script;
using Serein.Script.Node.FlowControl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.NodeFlow.Model.Nodes
{
    
    [FlowDataProperty(ValuePath = NodeValuePath.Node,  IsNodeImp = true)]
    public partial class SingleScriptNode : NodeModelBase
    {
        [DataInfo(IsNotification = true)]
        private string _script = string.Empty;
    }

    /// <summary>
    /// 流程脚本节点
    /// </summary>
    public partial class SingleScriptNode : NodeModelBase
    {
        /// <summary>
        /// 脚本节点是基础节点
        /// </summary>
        public override bool IsBase => true;

        private bool IsScriptChanged = false;

        /// <summary>
        /// 脚本解释器
        /// </summary>
        private readonly SereinScript sereinScript;

        /// <summary>
        /// 构建流程脚本节点
        /// </summary>
        /// <param name="environment"></param>
        public SingleScriptNode(IFlowEnvironment environment) : base(environment)
        {
            sereinScript = new SereinScript();
        }

        static SingleScriptNode()
        {
            // 挂载静态方法
            var tempMethods = typeof(Serein.Library.ScriptBaseFunc).GetMethods().Where(method =>
                    method.IsStatic && 
                    !(method.Name.Equals("GetHashCode")
                    || method.Name.Equals("Equals")
                    || method.Name.Equals("ToString")
                    || method.Name.Equals("GetType")
            )).Select(method => (method.Name, method)).ToArray();
            // 加载基础方法
            foreach ((string name, MethodInfo method) item in tempMethods)
            {
                SereinScript.AddStaticFunction(item.name, item.method);
            }
        }

        /// <summary>
        /// 代码改变后
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="NotImplementedException"></exception>
        partial void OnScriptChanged(string value)
        {
            IsScriptChanged = true;
        }

         /// <summary>
         /// 节点创建时
         /// </summary>
        public override void OnCreating()
        {
            var md = MethodDetails;
            var pd = md.ParameterDetailss ??= new ParameterDetails[1];
            md.ParamsArgIndex = 0;
            pd[0] =  new ParameterDetails
            {
                Index = 0,
                Name = "arg",
                IsExplicitData = true,
                DataValue = string.Empty,
                DataType = typeof(string),
                ExplicitType = typeof(string),
                ArgDataSourceNodeGuid = string.Empty,
                ArgDataSourceType = ConnectionArgSourceType.GetPreviousNodeData,
                NodeModel = this,
                InputType = ParameterValueInputType.Input,
                Items = null,
                IsParams = true,
                //Description = "脚本节点入参"
            };
            md.ReturnType = typeof(void); // 默认无返回

        }

        /// <summary>
        /// 更新节点返回类型
        /// </summary>
        /// <param name="newType"></param>
        private void UploadNodeReturnType(Type newType)
        {
            MethodDetails.ReturnType = newType;


            foreach (var ct in NodeStaticConfig.ConnectionArgSourceTypes)
            {
                var newResultNodes = NeedResultNodes[ct].ToArray();
                foreach (var node in newResultNodes)
                {
                    if(node is SingleScriptNode scriptNode)
                    {
                        var pds = scriptNode.MethodDetails.ParameterDetailss;
                        foreach (var pd in pds)
                        {
                            if (pd.ArgDataSourceType == ct &&
                                pd.ArgDataSourceNodeGuid == this.Guid)
                            {
                                pd.DataType = newType; // 更新参数类型
                            }
                        }
                        //scriptNode.ReloadScript(); // 重新加载目标脚本节点
                    }
                    
                }
            }
        }

        /// <summary>
        /// 保存项目时保存脚本代码、方法入参类型、返回值类型
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public override NodeInfo SaveCustomData(NodeInfo nodeInfo)
        {
            var paramsTypeName = MethodDetails.ParameterDetailss.Select(pd =>
            {
                return new ScriptArgInfo
                {
                    Index = pd.Index,
                    ArgName = pd.Name,
                    ArgType = pd.DataType.FullName,
                };
            }).ToArray();

            dynamic data = new ExpandoObject();
            data.Script = Script ?? "";
            data.ParamsTypeName = paramsTypeName;
            data.ReturnTypeName = MethodDetails.ReturnType;
            nodeInfo.CustomData = data;
            return nodeInfo;
        }

        private class ScriptArgInfo
        { 
            public int Index { get; set; }
            public string? ArgName { get; set; }
            public string? ArgType { get; set; }
        }


        /// <summary>
        /// 加载自定义数据
        /// </summary>
        /// <param name="nodeInfo"></param>
        public override void LoadCustomData(NodeInfo nodeInfo)
        {
            this.Script = nodeInfo.CustomData?.Script ?? "";
            

            var paramCount = Math.Min(MethodDetails.ParameterDetailss.Length, nodeInfo.ParameterData.Length);
            // 更新变量名
            for (int i = 0; i < paramCount; i++)
            {
                var pd = MethodDetails.ParameterDetailss[i];
                pd.Name = nodeInfo.ParameterData[i].ArgName;
            }

            try
            {
                string paramsTypeNameJson = nodeInfo.CustomData?.ParamsTypeName.ToString() ?? "[]";
                ScriptArgInfo[] array = JsonHelper.Deserialize<ScriptArgInfo[]>(paramsTypeNameJson);

                string returnTypeName = nodeInfo.CustomData?.ReturnTypeName ?? typeof(object);
                
                var flowLibService = Env.IOC.Get<IFlowLibraryService>();
                
                Type?[] argType = array.Select(info => string.IsNullOrWhiteSpace(info.ArgType) ? typeof(Unit) 
                                        : Type.GetType(info.ArgType) 
                                        ?? flowLibService.GetType(info.ArgType) 
                                        ?? typeof(Unit)).ToArray();

                Type? resType = Type.GetType(returnTypeName);
                for (int i = 0; i < paramCount; i++)
                {
                    var pd = MethodDetails.ParameterDetailss[i];
                    pd.DataType = argType[i];
                }
                MethodDetails.ReturnType = resType;
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR ,$"加载脚本自定义数据类型信息时发生异常：{ex.Message}");
            }

            //ReloadScript();// 加载时重新解析
            IsScriptChanged = false; // 重置脚本改变标志
        }


        private object reloadLockObj = new object(); 
        /// <summary>
        /// 重新加载脚本代码
        /// </summary>
        public bool ReloadScript()
        {
            lock (reloadLockObj)
            {
                if (!CheckRepeatParamter()) 
                    return false;
                var argTypes = GetParamterTypeInfo();
                try
                {
                    var returnType = sereinScript.ParserScript(Script, argTypes);  // 开始解析获取程序主节点
                    UploadNodeReturnType(returnType);
                }
                catch (Exception ex)
                {
                    SereinEnv.WriteLine(InfoType.WARN, ex.Message);
                    return false; // 解析失败
                }
                return true;
            }
        }

        /// <summary>
        /// 检查脚本参数是否有重复的名称
        /// </summary>
        /// <returns></returns>
        private bool CheckRepeatParamter()
        {
            bool isSueccess = true;
            HashSet<string> varNames = new HashSet<string>();
            foreach (var pd in MethodDetails.ParameterDetailss)
            {
                if (varNames.Contains(pd.Name))
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"脚本节点重复的变量名称：{pd.Name} - {Guid}");

                    isSueccess = false;
                }
                varNames.Add(pd.Name);
            }
            return isSueccess;
        }

        /// <summary>
        /// 获取参数类型信息
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, Type> GetParamterTypeInfo()
        {
            Dictionary<string, Type> argTypes = [];
            var pds = MethodDetails.ParameterDetailss.ToArray();
            foreach (var pd in pds)
            {
                if (pd.ArgDataSourceType == ConnectionArgSourceType.GetPreviousNodeData)
                {
                    argTypes[pd.Name] = pd.DataType;
                }
                if (Env.TryGetNodeModel(pd.ArgDataSourceNodeGuid, out var node) &&
                    node.MethodDetails?.ReturnType is not null)
                {
                    pd.DataType = node.MethodDetails.ReturnType;
                    var Type = node.MethodDetails.ReturnType;
                    argTypes[pd.Name] = Type;
                }
            }
            return argTypes;
        }

        /// <summary>
        /// 转换为 C# 代码，并且附带方法信息
        /// </summary>
        public SereinScriptMethodInfo? ToCsharpMethodInfo(string methodName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(methodName))
                {
                    var tmp = Guid.Replace("-", "");
                     methodName = $"FlowMethod_{tmp}";
                }

                if (!CheckRepeatParamter())
                        throw new Exception($"脚本节点入参重复");
                var argTypes = GetParamterTypeInfo();
                var returnType = sereinScript.ParserScript(Script, argTypes);  // 开始解析获取程序主节点
                UploadNodeReturnType(returnType);
                var scriptMethodInfo =  sereinScript.ConvertCSharpCode(methodName, argTypes);
                return scriptMethodInfo;
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.WARN, ex.Message);
                return null; // 解析失败
            }
        }

        /// <summary>
        /// 执行脚本
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public override async Task<FlowResult> ExecutingAsync(IFlowContext context, CancellationToken token)
        {
            if (IsScriptChanged)
            {
                if (!ReloadScript())
                {
                    return FlowResult.Fail(this.Guid, context, "脚本解析失败，请检查脚本代码");
                }
            }
            var result = await ExecutingAsync(this, context, token);
            return result;
        }

        /// <summary>
        /// 流程接口提供参数进行调用脚本节点
        /// </summary>
        /// <param name="flowCallNode"></param>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<FlowResult> ExecutingAsync(NodeModelBase flowCallNode,  IFlowContext context, CancellationToken token)
        {
            if (token.IsCancellationRequested) return FlowResult.Fail(this.Guid, context, "流程已通过token取消");
            if (IsScriptChanged)
            {
                if (!ReloadScript())
                {
                    return FlowResult.Fail(this.Guid, context, "脚本解析失败，请检查脚本代码");
                }
            }

            var @params = await flowCallNode.GetParametersAsync(context, token);

            IScriptInvokeContext scriptContext = new ScriptInvokeContext();

            if (@params[0] is object[] agrDatas)
            {
                for (int i = 0; i < agrDatas.Length; i++)
                {
                    var argName = flowCallNode.MethodDetails.ParameterDetailss[i].Name;
                    var argData = agrDatas[i];
                    scriptContext.SetVarValue(argName, argData);
                }
            }

            FlowRunCompleteHandler onFlowStop = (e) =>
            {
                scriptContext.OnExit();
            };

            var envEvent = context.Env.Event;
            envEvent.FlowRunComplete += onFlowStop; // 防止运行后台流程

            if (token.IsCancellationRequested) return FlowResult.Fail(this.Guid, context, "流程已通过token取消");

            var result = await sereinScript.InterpreterAsync(scriptContext); // 从入口节点执行
            envEvent.FlowRunComplete -= onFlowStop;
            return FlowResult.OK(this.Guid, context, result);
        }

    }
}
