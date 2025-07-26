namespace Serein.Script
{
    public class SereinScriptMethodInfo
    {
        public string ClassName { get; set; }
        public string MethodName { get; set; }

        public Type? ReturnType { get; set; }

        public bool IsAsync { get; set; }

        public List<SereinScriptParamInfo> ParamInfos { get; set; }

        public string CsharpCode { get; set; }

        public class SereinScriptParamInfo
        {
            public string ParamName { get; set; }
            public Type ParameterType { get; set; }
        }
    }
}
