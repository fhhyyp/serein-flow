using Serein.Library;
using Serein.Script.Node;
using Serein.Script.Node.FlowControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script
{
    public class SereinScript
    {
        /// <summary>
        /// 类型分析
        /// </summary>
        public SereinScriptTypeAnalysis TypeAnalysis { get; set; } = new SereinScriptTypeAnalysis();



        private ProgramNode? programNode;

        /// <summary>
        /// 解析脚本
        /// </summary>
        /// <param name="script">脚本</param>
        /// <param name="argTypes">挂载的变量</param>
        /// <returns></returns>
        public Type ParserScript(string script, Dictionary<string, Type>? argTypes = null)
        {
            SereinScriptParser parser = new SereinScriptParser();
            var programNode =  parser.Parse(script);
            TypeAnalysis.NodeSymbolInfos.Clear(); // 清空符号表
            if(argTypes is not null) TypeAnalysis.LoadSymbol(argTypes); // 提前加载脚本节点定义的符号
            TypeAnalysis.Analysis(programNode); // 分析节点类型
            var returnType = TypeAnalysis.NodeSymbolInfos[programNode]; // 获取返回类型
            this.programNode = programNode;
            return returnType; // 脚本返回类型
        }

        public async Task<object?> InterpreterAsync(IScriptInvokeContext context)
        {
            if(programNode is null)
            {
                throw new ArgumentNullException(nameof(programNode));
            }
            Dictionary<ASTNode, Type> symbolInfos = TypeAnalysis.NodeSymbolInfos.ToDictionary();
            SereinScriptInterpreter Interpreter = new SereinScriptInterpreter(symbolInfos);
            return await Interpreter.InterpretAsync(context, programNode);
        }







        /// <summary>
        /// 挂载的函数
        /// </summary>
        public static Dictionary<string, DelegateDetails> FunctionDelegates { get; private set; } = [];

        /// <summary>
        /// 挂载方法的信息
        /// </summary>
        public static Dictionary<string, MethodInfo> FunctionInfos { get; private set; } = [];

        /// <summary>
        /// 挂载的类型
        /// </summary>
        public static Dictionary<string, Type> MountType = new Dictionary<string, Type>();

        /// <summary>
        /// 挂载的函数调用的对象（用于解决函数需要实例才能调用的场景）
        /// </summary>
        public static Dictionary<string, Func<object>> DelegateInstances = new Dictionary<string, Func<object>>();

        /// <summary>
        /// 挂载静态函数
        /// </summary>
        /// <param name="functionName"></param>
        /// <param name="methodInfo"></param>
        public static void AddStaticFunction(string functionName, MethodInfo methodInfo)
        {
            FunctionDelegates[functionName] = new DelegateDetails(methodInfo);
            FunctionInfos[functionName] = methodInfo;
        }


        /// <summary>
        /// 挂载函数
        /// </summary>
        /// <param name="functionName">函数名称</param>
        /// <param name="methodInfo">方法信息</param>
        public static void AddFunction(string functionName, MethodInfo methodInfo, Func<object>? callObj = null)
        {
            if (!methodInfo.IsStatic && callObj is null)
            {
                SereinEnv.WriteLine(InfoType.WARN, "函数挂载失败：试图挂载非静态的函数，但没有传入相应的获取实例的方法。");
                return;
            }

            if (!methodInfo.IsStatic && callObj is not null && !DelegateInstances.ContainsKey(functionName))
            {
                // 非静态函数需要给定类型
                DelegateInstances.Add(functionName, callObj);
            }
            if (!FunctionDelegates.ContainsKey(functionName))
            {
                FunctionDelegates[functionName] = new DelegateDetails(methodInfo);
            }
        }

        /// <summary>
        /// 挂载类型
        /// </summary>
        /// <param name="typeName">函数名称</param>
        /// <param name="typeName">指定类型名称</param>
        public static void AddClassType(Type type, string typeName = "")
        {
            if (string.IsNullOrEmpty(typeName))
            {
                typeName = type.Name;
            }
            if (!MountType.ContainsKey(typeName))
            {
                MountType[typeName] = type;
            }
        }





    }
}
