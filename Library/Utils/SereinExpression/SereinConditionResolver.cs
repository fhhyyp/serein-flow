using System.Reflection;

namespace Serein.NodeFlow.Tool.SereinExpression
{
    /// <summary>
    /// 条件解析抽象类
    /// </summary>
    public abstract class SereinConditionResolver
    {
        public abstract bool Evaluate(object obj);
    }
}
