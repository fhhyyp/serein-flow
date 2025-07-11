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

        private IScriptFlowApi ScriptFlowApi;
        private ProgramNode programNode;
        private SereinScriptInterpreter ScriptInterpreter;
        private bool IsScriptChanged = false;

        /// <summary>
        /// 构建流程脚本节点
        /// </summary>
        /// <param name="environment"></param>
        public SingleScriptNode(IFlowEnvironment environment):base(environment) 
        {
            ScriptFlowApi = new ScriptFlowApi(environment, this);
            ScriptInterpreter = new SereinScriptInterpreter();
        }

        static SingleScriptNode()
        {
            // 挂载静态方法
            var tempMethods = typeof(ScriptBaseFunc).GetMethods().Where(method =>
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
                //Description = "脚本节点入参"

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

            ReloadScript();// 加载时重新解析
            IsScriptChanged = false; // 重置脚本改变标志

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

                var sb  = new StringBuilder();
                foreach (var pd in MethodDetails.ParameterDetailss)
                {
                    sb.AppendLine($"let {pd.Name};"); // 提前声明这些变量
                }
                sb.Append(Script);
                var script = sb.ToString();
                var p = new SereinScriptParser(script);
                //var p = new SereinScriptParser(Script);
                programNode = p.Parse(); // 开始解析
                var typeAnalysis = new SereinScriptTypeAnalysis();
                ScriptInterpreter.SetTypeAnalysis(typeAnalysis);
                typeAnalysis.AnalysisProgramNode(programNode);

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
        public override async Task<FlowResult> ExecutingAsync(IDynamicContext context, CancellationToken token)
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
        public async Task<FlowResult> ExecutingAsync(NodeModelBase flowCallNode,  IDynamicContext context, CancellationToken token)
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
                        ReloadScript();// 每次都重新解析
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


            var result = await ScriptInterpreter.InterpretAsync(scriptContext, programNode); // 从入口节点执行
            envEvent.FlowRunComplete -= onFlowStop;
            return new FlowResult(this.Guid, context, result);
        }

        #region 挂载的方法

        public IScriptFlowApi GetFlowApi()
        {
            return ScriptFlowApi;
        }

        private static class ScriptBaseFunc
        {
            public static DateTime GetNow() => DateTime.Now;

            public static int Add(int Left, int Right)
            {
                return Left + Right;
            }

            #region 常用的类型转换
            public static bool @bool(object value)
            {
                return ConvertHelper.ValueParse<bool>(value);
            }
            public static decimal @decimal(object value)
            {
                return ConvertHelper.ValueParse<decimal>(value);
            }
            public static float @float(object value)
            {
                return ConvertHelper.ValueParse<float>(value);
            }
            public static double @double(object value)
            {
                return ConvertHelper.ValueParse<double>(value);
            }
            public static int @int(object value)
            {
                return ConvertHelper.ValueParse<int>(value);
            }
            public static int @long(object value)
            {
                return ConvertHelper.ValueParse<int>(value);
            }
            

            #endregion

            public static int len(object target)
            {
                // 获取数组或集合对象
                // 访问数组或集合中的指定索引
                if (target is Array array)
                {
                    return array.Length;
                }
                else if (target is string chars)
                {
                    return chars.Length;
                }
                else if (target is IDictionary<object, object> dict)
                {
                    return dict.Count;
                }
                else if (target is IList<object> list)
                {
                    return list.Count;
                }
                else
                {
                    throw new ArgumentException($"并非有效集合");
                }
            }
            
            public static Type type(object type)
            {
                return type.GetType();
            }

            public static void log(object value)
            {
                SereinEnv.WriteLine(InfoType.INFO, value?.ToString());
            }

            public static async Task sleep(object value)
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
