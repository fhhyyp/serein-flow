using Serein.Library.Api;

namespace Serein.Script
{
    public sealed class ScriptInvokeContext : IScriptInvokeContext
    {
        /// <summary>
        /// 脚本使用流程上下文
        /// </summary>
        /// <param name="flowContext"></param>
        public ScriptInvokeContext(IFlowContext flowContext)
        {
            FlowContext = flowContext;
        }
        
        /// <summary>
        /// 不使用流程上下文
        /// </summary>
        public ScriptInvokeContext()
        {
        }

#pragma warning disable CS8766 // 返回类型中引用类型的为 Null 性与隐式实现的成员不匹配(可能是由于为 Null 性特性)。
        public IFlowContext? FlowContext{ get; }
#pragma warning restore CS8766 // 返回类型中引用类型的为 Null 性与隐式实现的成员不匹配(可能是由于为 Null 性特性)。

        /// <summary>
        /// 定义的变量
        /// </summary>
        private Dictionary<string, object?> _variables = new Dictionary<string, object?>();

        /// <summary>
        /// 取消令牌源，用于控制脚本的执行
        /// </summary>
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        /// <summary>
        /// 是否该退出了
        /// </summary>
        public bool IsReturn => _tokenSource.IsCancellationRequested;

        /// <summary>
        /// 是否严格检查 Null 值 （禁止使用 Null）
        /// </summary>
        public bool IsCheckNullValue { get; set; }

        /// <summary>
        ///  是否需要提前返回（用于脚本中提前结束）
        /// </summary>
        public bool IsNeedReturn { get; set; }


        /// <summary>
        /// 获取变量的值
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        object? IScriptInvokeContext.GetVarValue(string varName)
        {
            _variables.TryGetValue(varName, out var value);
            return value;
        }


        /// <summary>
        /// 设置变量的值
        /// </summary>
        /// <param name="varName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        bool IScriptInvokeContext.SetVarValue(string varName, object? value)
        {
            if (!_variables.TryAdd(varName, value))
            {
                _variables[varName] = value;
            }
            return true;
        }

        
        void IScriptInvokeContext.OnExit()
        {
            // 清理脚本中加载的非托管资源
            foreach (var nodeObj in _variables.Values)
            {
                if (nodeObj is not null)
                {
                    if (typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) && nodeObj is IDisposable disposable)
                    {
                        disposable?.Dispose();
                    }
                }
                else
                {

                }
            }
            _tokenSource.Cancel();
            _variables.Clear();
        }

    }
}
