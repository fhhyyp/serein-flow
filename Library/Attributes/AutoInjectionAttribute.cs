using System;

namespace Serein.Library
{
    /// <summary>
    /// <para>表示该属性为自动注入依赖项。</para>
    /// <para>使用场景：构造函数中存在互相依赖的情况</para>
    /// <para>例如ServiceA类构造函数中需要传入ServiceB，ServiceB类构造函数中也需要传入ServiceA</para>
    /// <para>这种情况会导致流程启动时，IOC容器无法注入构造函数并创建类型，导致启动失败。</para>
    /// <para>解决方法：从ServiceA类的构造函数中移除ServiceB类型的入参，将该类型更改为公开可见的可写属性成员ServiceB serviceB{get;set;}，并在该属性上标记[AutoInjection]特性</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class AutoInjectionAttribute : Attribute
    {
    }

}
