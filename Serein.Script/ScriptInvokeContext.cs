using Serein.Library.Api;

namespace Serein.Script
{
    public class ScriptInvokeContext : IScriptInvokeContext
    {
        public ScriptInvokeContext(IDynamicContext dynamicContext)
        {
            FlowContext = dynamicContext;
        }

        public IDynamicContext FlowContext{ get; }

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


        object IScriptInvokeContext.GetVarValue(string varName)
        {
            _variables.TryGetValue(varName, out var value);
            return value;
        }


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
