using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Script;
using Serein.Script.Node;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private IScriptFlowApi ScriptFlowApi { get; }

        private ASTNode mainNode;

        /// <summary>
        /// 构建流程脚本节点
        /// </summary>
        /// <param name="environment"></param>
        public SingleScriptNode(IFlowEnvironment environment):base(environment) 
        {
            //ScriptFlowApi = environment.IOC.Get<ScriptFlowApi>();
            ScriptFlowApi = new ScriptFlowApi(environment, this);
            

            MethodInfo? method = this.GetType().GetMethod(nameof(GetFlowApi));
            if (method != null)
            {
                SereinScriptInterpreter.AddFunction(nameof(GetFlowApi), method, () => this); // 挂载获取流程接口
            }

            // 挂载静态方法
            var tempMethods = typeof(BaseFunc).GetMethods().Where(method =>
                    !(method.Name.Equals("GetHashCode")
                    || method.Name.Equals("Equals")
                    || method.Name.Equals("ToString")
                    || method.Name.Equals("GetType")
            )).Select(method => (method.Name, method)).ToArray();

            foreach ((string name, MethodInfo method) item in tempMethods)
            {
                SereinScriptInterpreter.AddFunction(item.name, item.method); // 加载基础方法
            }
        }

        /// <summary>
        /// 加载脚本代码
        /// </summary>
        public void LoadScript()
        {
            try
            {
                mainNode = new SereinScriptParser(Script).Parse();
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

            mainNode ??= new SereinScriptParser(Script).Parse();
            SereinScriptInterpreter scriptInterpreter = new SereinScriptInterpreter();
            var result = await scriptInterpreter.InterpretAsync(mainNode); // 从入口节点执行
            scriptInterpreter.ResetVar();
            return result;
        }

        
        public IScriptFlowApi GetFlowApi() 
        { 
            return ScriptFlowApi; 
        }

        private static class BaseFunc
        {
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
    }
}
