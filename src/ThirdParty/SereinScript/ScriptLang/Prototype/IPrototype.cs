using ScriptLang.Runtime;

namespace ScriptLang
{
    /// <summary>
    /// 原型接口
    /// </summary>
    public interface IPrototype
    {
        /// <summary>
        /// 是否已加载
        /// </summary>
        bool IsLoad { get; }

        /// <summary>
        /// 初始化原型
        /// </summary>
        void Init();

        /// <summary>
        /// 判断值是否为目标类型
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        bool IsTarget(Value value);

        /// <summary>
        /// 获取方法
        /// </summary>
        /// <param name="value">传入的值</param>
        /// <param name="methodName">方法名</param>
        /// <param name="engine">脚本引擎</param>
        /// <returns>方法值，如果不存在则返回 null</returns>
        Value? GetMethod(Value value, string methodName, ScriptEngine engine);
    }


}
