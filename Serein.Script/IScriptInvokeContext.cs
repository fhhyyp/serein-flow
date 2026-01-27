using Serein.Library.Api;

namespace Serein.Script
{
    /// <summary>
    /// 脚本运行上下文
    /// </summary>
    public interface IScriptInvokeContext
    {

        /// <summary>
        /// 是否需要提前返回（用于脚本中提前结束）
        /// </summary>
        bool IsReturn { get; set; }

        /// <summary>
        /// 是否严格检查 Null 值 （禁止使用 Null）
        /// </summary>
        bool IsCheckNullValue { get; set; }

        /// <summary>
        /// 获取变量的值
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        object? GetVarValue(string varName);

        /// <summary>
        /// 设置变量的值
        /// </summary>
        /// <param name="varName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        bool SetVarValue(string varName, object? value);

        /// <summary>
        /// 结束调用
        /// </summary>
        /// <returns></returns>
        void OnExit();
    }
}
