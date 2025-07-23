using Serein.Library;
using Serein.Library.Api;
using Serein.Script;
using Serein.Script.Node.FlowControl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.NodeFlow.Model
{
    
    [NodeProperty(ValuePath = NodeValuePath.Node)]
    public partial class SingleScriptNode : NodeModelBase
    {
        [PropertyInfo(IsNotification = true)]
        private string _script;
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
        /// 导出脚本代码
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public override NodeInfo SaveCustomData(NodeInfo nodeInfo)
        {
            dynamic data = new ExpandoObject();
            data.Script = Script ?? "";
            nodeInfo.CustomData = data;
            return nodeInfo;
        }

        /// <summary>
        /// 加载自定义数据
        /// </summary>
        /// <param name="nodeInfo"></param>
        public override void LoadCustomData(NodeInfo nodeInfo)
        {
            this.Script = nodeInfo.CustomData?.Script ?? "";

            // 更新变量名
            for (int i = 0; i < Math.Min(this.MethodDetails.ParameterDetailss.Length, nodeInfo.ParameterData.Length); i++)
            {
                this.MethodDetails.ParameterDetailss[i].Name = nodeInfo.ParameterData[i].ArgName;
            }

            //ReloadScript();// 加载时重新解析
            IsScriptChanged = false; // 重置脚本改变标志

        }

        /// <summary>
        /// 重新加载脚本代码
        /// </summary>
        public bool ReloadScript()
        {
            try
            {
                HashSet<string> varNames = new HashSet<string>();   
                foreach (var pd in MethodDetails.ParameterDetailss) 
                { 
                    if (varNames.Contains(pd.Name))
                    {
                        throw new Exception($"脚本节点重复的变量名称：{pd.Name} - {Guid}");
                    }
                    varNames.Add(pd.Name);
                }


                var argTypes = MethodDetails.ParameterDetailss
                                       .Select(pd =>
                                       {
                                           if(pd.ArgDataSourceType == ConnectionArgSourceType.GetPreviousNodeData)
                                           {
                                               var Type = pd.DataType;
                                               return (pd.Name, Type);
                                           }
                                           if (Env.TryGetNodeModel(pd.ArgDataSourceNodeGuid, out var node) &&
                                               node.MethodDetails?.ReturnType is not null)
                                           {
                                               pd.DataType = node.MethodDetails.ReturnType;
                                               var Type = node.MethodDetails.ReturnType;
                                               return (pd.Name, Type);
                                           }
                                           return default;
                                       })
                                       .Where(x => x != default)
                                       .ToDictionary(x => x.Name, x => x.Type); // 准备预定义类型


                var returnType = sereinScript.ParserScript(Script, argTypes);  // 开始解析获取程序主节点
                
                MethodDetails.ReturnType = returnType;

                var code = sereinScript.ConvertCSharpCode("Test", argTypes);
                Debug.WriteLine(code);
                return true;
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.WARN, ex.Message);
                return false; // 解析失败
            }
        }

        /// <summary>
        /// 执行脚本
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<FlowResult> ExecutingAsync(IFlowContext context, CancellationToken token)
        {
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
            if (token.IsCancellationRequested) return new FlowResult(this.Guid, context);
            var @params = await flowCallNode.GetParametersAsync(context, token);
            if (token.IsCancellationRequested) return new FlowResult(this.Guid, context);

            //context.AddOrUpdate($"{context.Guid}_{this.Guid}_Params", @params[0]); // 后面再改

            if (IsScriptChanged)
            {
                lock (@params) {
                    if (IsScriptChanged)
                    {
                        ReloadScript();// 执行时检查是否需要重新解析
                        IsScriptChanged = false;
                        context.Env.WriteLine(InfoType.INFO, $"[{Guid}]脚本解析完成");
                    }
                }
            }

            IScriptInvokeContext scriptContext = new ScriptInvokeContext(context);

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

            if (token.IsCancellationRequested) return null;

            var result = await sereinScript.InterpreterAsync(scriptContext); // 从入口节点执行
            envEvent.FlowRunComplete -= onFlowStop;
            return new FlowResult(this.Guid, context, result);
        }

    }
}
