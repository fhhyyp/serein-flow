namespace Serein.Script
{
    /// <summary>
    /// 脚本方法信息
    /// </summary>
    public class SereinScriptMethodInfo
    {
        /// <summary>
        /// 类名
        /// </summary>
        public string ClassName { get; set; }
        
        /// <summary>
        /// 方法名
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// 返回类型
        /// </summary>
        public Type? ReturnType { get; set; }

        /// <summary>
        /// 是否异步
        /// </summary>
        public bool IsAsync { get; set; }

        /// <summary>
        /// 入参参数信息
        /// </summary>
        public List<SereinScriptParamInfo> ParamInfos { get; set; }

        /// <summary>
        /// 对应的C#代码
        /// </summary>
        public string CsharpCode { get; set; }

        /// <summary>
        /// 入参信息
        /// </summary>
        public class SereinScriptParamInfo
        {
            /// <summary>
            /// 入参参数名称
            /// </summary>
            public string ParamName { get; set; }
            
            /// <summary>
            /// 入参类型
            /// </summary>
            public Type ParameterType { get; set; }
        }
    }
}
