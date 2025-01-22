using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Tool
{
    internal class ToCSharpCodeHelper
    {
        /// <summary>
        /// 运行环境
        /// </summary>
        private readonly IFlowEnvironment env;
        /// <summary>
        /// 环境中已加载的所有节点
        /// </summary>
        private readonly List<NodeModelBase> nodes;
        /// <summary>
        /// 获取流程启动时在不同时间点需要自动实例化的类型
        /// </summary>
        private readonly Dictionary<RegisterSequence, List<Type>> autoRegisterTypes;
        /// <summary>
        /// 初始化方法
        /// </summary>
        private readonly List<MethodDetails> initMethods;
        /// <summary>
        /// 加载时方法
        /// </summary>
        private readonly List<MethodDetails> loadingMethods;
        /// <summary>
        /// 结束时方法
        /// </summary>
        private readonly List<MethodDetails> exitMethods;

        /// <summary>
        /// 开始运行（需要准备好方法信息）
        /// </summary>
        /// <param name="env">运行环境</param>
        /// <param name="nodes">环境中已加载的所有节点</param>
        /// <param name="autoRegisterTypes">获取流程启动时在不同时间点需要自动实例化的类型</param>
        /// <param name="initMethods">初始化方法</param>
        /// <param name="loadingMethods">加载时方法</param>
        /// <param name="exitMethods">结束时方法</param>
        /// <returns></returns>
        public ToCSharpCodeHelper(IFlowEnvironment env,
                                   List<NodeModelBase> nodes,
                                   Dictionary<RegisterSequence, List<Type>> autoRegisterTypes,
                                   List<MethodDetails> initMethods,
                                   List<MethodDetails> loadingMethods,
                                   List<MethodDetails> exitMethods)
        {
            this.env = env;
            this.nodes = nodes;
            this.autoRegisterTypes = autoRegisterTypes;
            this.initMethods = initMethods;
            this.loadingMethods = loadingMethods;
            this.exitMethods = exitMethods;
        }
        
        public string ToCode()
        {
            StringBuilder sb = new StringBuilder();

            // 确认命名空间
            sb.AppendLine($"using System;");
            sb.AppendLine($"using System.Linq;");
            sb.AppendLine($"using System.Threading;");
            sb.AppendLine($"using System.Threading.Tasks;");
            sb.AppendLine($"using System.Collections.Concurrent;");
            sb.AppendLine($"using System.Collections.Generic;");
            sb.AppendLine($"using Serein.Library;");
            sb.AppendLine($"using Serein.Library.Api;");
            sb.AppendLine($"using Serein.NodeFlow;");
            sb.AppendLine(
            """
            using System;
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            using System.Collections.Concurrent;
            using System.Collections.Generic;
            using Serein.Library;
            using Serein.Library.Api;
            using Serein.NodeFlow;

            // 这里添加引用

            namespace NodeToCode
            {
                class Program
                {
                   private readonly IFlowEnvironment env; // 流程运行环境
            """ +
                    DefineVariableCode() + // 这里定义变量
            """
                    public void Main(string[] args)
                    {
            """ +
                        MainOfCSharpCode() + // 这里初始化运行环境
            """
                    }
            """ +
                    BusinessOfCSharpCode() + //  流程逻辑代码
            """
                }
            }
            """
            );
            //sb.AppendLine($"public {returnType} {methodName}({parmasStr})");
            //sb.AppendLine($"{{");
            //sb.AppendLine($"}}");

            return "";
        }

        public void Main(string[] args) {
            var plcLoginControl = ioc.Get<PlcLoginControl>();
            var networkLoginControl = ioc.Get<PlcLoginControl>();

            if(nodeModel.)
            plcLoginControl.Init("", networkLoginControl.Func("",""))

            foreach (var md in ffMd)
            {
                Execution(md);
            }


             plcLoginControl.Init("129.123.41.21",123);
            
        }

        public void Execution(Action action)
        {

        }
        public object Execution(Func<object> action)
        {

        }
        public async object Execution(Func<Task> task)
        {
            await task.Invoke();
        }

        private string MainOfCSharpCode()
        {

            return "";
        }
        private string BusinessOfCSharpCode()
        {
            // 定义变量

            // 确定需要实例化的类型

            // 实例化方法

            // 确认调用的方法

            // 确认开辟的线程
            return "";
        }

        private string DefineVariableCode()
        {

        }
        /*
       
         */


        public StringBuilder ToMethodCode(StringBuilder sb, string methodName, Type returnType, Type[] parmas)
        {
            var parmasStr = string.Join("," + Environment.NewLine, parmas.Select(t => $"{t} {t.Name.Replace('.', '_')}"));
            sb.AppendLine($"public {returnType} {methodName}({parmasStr})");
            sb.AppendLine($"{{");
            sb.AppendLine($"}}");
            return sb;
        }

        /// <summary>
        ///  添加代码
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="retractCount">缩进次数（4个空格）</param>
        /// <param name="code">要添加的代码</param>
        /// <returns>字符串构建器本身</returns>
        private static StringBuilder AddCode(StringBuilder sb,
            int retractCount = 0,
            string code = "")
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                var retract = new string(' ', retractCount * 4);
                sb.AppendLine(retract + code);
            }
            return sb;
        }

        public static IEnumerable<string> SeparateLineIntoMultipleDefinitions(ReadOnlySpan<char> line)
        {
            List<string> definitions = new List<string>();

            bool captureIsStarted = false;

            int equalSignCount = 0;
            int lastEqualSignPosition = 0;
            int captureStart = 0;
            int captureEnd = 0;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c != ',' && !char.IsWhiteSpace(c))
                {
                    if (captureIsStarted)
                    {
                        captureEnd = i;
                    }

                    else
                    {
                        captureStart = i;
                        captureIsStarted = true;
                    }

                    if (c == '=')
                    {
                        equalSignCount++;
                        lastEqualSignPosition = i;
                    }
                }
                else
                {
                    if (equalSignCount == 1 && lastEqualSignPosition > captureStart && lastEqualSignPosition < captureEnd)
                    {
                        definitions.Add(line[captureStart..(captureEnd + 1)].ToString());
                    }

                    equalSignCount = 0;
                    captureIsStarted = false;
                }
            }

            if (captureIsStarted && equalSignCount == 1 && lastEqualSignPosition > captureStart && lastEqualSignPosition < captureEnd)
            {
                definitions.Add(line[captureStart..(captureEnd + 1)].ToString());
            }

            return definitions;
        }

    }
}
