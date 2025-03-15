using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Script;
using Serein.Script.Node;
using System;
using System.Collections.Generic;
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


        private IScriptFlowApi ScriptFlowApi { get; }

        private ASTNode mainNode;
        private SereinScriptInterpreter ScriptInterpreter;
        /// <summary>
        /// 构建流程脚本节点
        /// </summary>
        /// <param name="environment"></param>
        public SingleScriptNode(IFlowEnvironment environment):base(environment) 
        {
            //ScriptFlowApi = environment.IOC.Get<ScriptFlowApi>();
            ScriptFlowApi = new ScriptFlowApi(environment, this);
            ScriptInterpreter = new SereinScriptInterpreter();
        }

        static SingleScriptNode()
        {
            // 挂载静态方法
            var tempMethods = typeof(BaseFunc).GetMethods().Where(method =>
                    !(method.Name.Equals("GetHashCode")
                    || method.Name.Equals("Equals")
                    || method.Name.Equals("ToString")
                    || method.Name.Equals("GetType")
            )).Select(method => (method.Name, method)).ToArray();
            // 加载基础方法
            foreach ((string name, MethodInfo method) item in tempMethods)
            {
                SereinScriptInterpreter.AddStaticFunction(item.name, item.method);
            }
        }


        public override void OnCreating()
        {
            MethodInfo? method = this.GetType().GetMethod(nameof(GetFlowApi));
            if (method != null)
            {
                ScriptInterpreter.AddFunction(nameof(GetFlowApi), method, () => this); // 挂载获取流程接口
            }

            var md = MethodDetails;
            var pd = md.ParameterDetailss ??= new ParameterDetails[1];
            md.ParamsArgIndex = 0;
            pd[0] =  new ParameterDetails
            {
                Index = 0,
                Name = "object",
                IsExplicitData = true,
                DataValue = string.Empty,
                DataType = typeof(object),
                ExplicitType = typeof(object),
                ArgDataSourceNodeGuid = string.Empty,
                ArgDataSourceType = ConnectionArgSourceType.GetPreviousNodeData,
                NodeModel = this,
                InputType = ParameterValueInputType.Input,
                Items = null,
                IsParams = true,
                Description = "脚本节点入参"

            };

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


        }

        /// <summary>
        /// 重新加载脚本代码
        /// </summary>
        public void ReloadScript()
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

                StringBuilder sb  = new StringBuilder();
                foreach (var pd in MethodDetails.ParameterDetailss)
                {
                    sb.AppendLine($"let {pd.Name};"); // 提前声明这些变量
                }
                sb.Append(Script);
                var p = new SereinScriptParser(sb.ToString());
                //var p = new SereinScriptParser(Script);
                mainNode = p.Parse(); // 开始解析
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, ex.ToString());
                
            }
        }

        /// <summary>
        /// 执行脚本
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<object?> ExecutingAsync(IDynamicContext context)
        {
            var @params =  await GetParametersAsync(context);
            

            //context.AddOrUpdate($"{context.Guid}_{this.Guid}_Params", @params[0]); // 后面再改
             ReloadScript();// 每次都重新解析

            IScriptInvokeContext scriptContext = new ScriptInvokeContext(context);

            if (@params[0] is object[] agrDatas)
            {
                for (int i = 0; i < agrDatas.Length; i++)
                {
                    var argName = MethodDetails.ParameterDetailss[i].Name;
                    var argData = agrDatas[i];
                    scriptContext.SetVarValue(argName, argData);
                }
            }

            
            FlowRunCompleteHandler onFlowStop = (e) =>
            {
                scriptContext.OnExit();
            };

            var envEvent = (IFlowEnvironmentEvent)context.Env;
            envEvent.OnFlowRunComplete += onFlowStop; // 防止运行后台流程
            var result = await ScriptInterpreter.InterpretAsync(scriptContext, mainNode); // 从入口节点执行
            envEvent.OnFlowRunComplete -= onFlowStop; 
            //SereinEnv.WriteLine(InfoType.INFO, "FlowContext Guid : " + context.Guid);
            return result;
        }


        #region 挂载的方法

        public IScriptFlowApi GetFlowApi()
        {
            return ScriptFlowApi;
        }

        private static class BaseFunc
        {
            public static DateTime GetNow() => DateTime.Now;

            public static Type TypeOf(object type)
            {
                return type.GetType();
            }


            public static void Print(object value)
            {
                SereinEnv.WriteLine(InfoType.INFO, value?.ToString());
            }

            #region 数据转换
            public static int ToInt(object value)
            {
                return int.Parse(value.ToString());
            }
            public static double ToDouble(object value)
            {
                return double.Parse(value.ToString());
            }
            public static bool ToBool(object value)
            {
                return bool.Parse(value.ToString());
            }
            #endregion

            public static async Task Delay(object value)
            {
                if (value is int @int)
                {
                    Console.WriteLine($"等待{@int}ms");
                    await Task.Delay(@int);
                }
                else if (value is TimeSpan timeSpan)
                {
                    Console.WriteLine($"等待{timeSpan}");
                    await Task.Delay(timeSpan);
                }

            }
        } 
        #endregion
    }
}
