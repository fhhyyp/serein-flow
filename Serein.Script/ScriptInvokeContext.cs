using Serein.Library.Api;

namespace Serein.Script
{
    public sealed class ScriptInvokeContext : IScriptInvokeContext
    {
        /// <summary>
        /// 不使用流程上下文
        /// </summary>
        public ScriptInvokeContext()
        {
        }

        /// <summary>
        /// 定义的变量
        /// </summary>
        private Dictionary<string, object?> _variables = new Dictionary<string, object?>();

        /// <summary>
        /// 取消令牌源，用于控制脚本的执行
        /// </summary>
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        /// <summary>
        /// 是否严格检查 Null 值 （禁止使用 Null）
        /// </summary>
        public bool IsCheckNullValue { get; set; }

        /// <summary>
        ///  是否需要提前返回（用于脚本中提前结束）
        /// </summary>
        public bool IsReturn { get; set; }


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
