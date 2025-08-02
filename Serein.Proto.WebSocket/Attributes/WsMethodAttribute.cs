namespace Serein.Proto.WebSocket.Attributes
{
    /// <summary>
    /// <para>作用：WebSocket中处理Json时，将通过Json中ThemeKey 对应的内容（ThemeValue）自动路由到相应方法进行处理，同时要求Data中必须存在对应入参。</para>
    /// <para>如果没有显式设置 ThemeValue，将默认使用方法名称作为ThemeValue。</para>
    /// <para>如果没有显式设置 IsReturnValue 标记为 false ，当方法顺利完成（没有抛出异常，且返回对象非null），会自动转为json文本发送回去</para>
    /// <para>如果没有显式设置 ArgNotNull 标记为 false ，当外部尝试调用时，若 Json Data 不包含响应的数据，将会被忽略此次调用</para>
    /// <para>如果返回类型为Task或Task&lt;TResult&gt;，将会自动等待异步完成并获取结果（无法处理Task&lt;Task&lt;TResult&gt;&gt;的情况）。</para>
    /// <para>如果返回了值类型，会自动装箱为引用对象。</para>
    /// <para>如果有方法执行过程中发送消息的需求，请在入参中声明以下类型的成员，调用时将传入发送消息的委托。</para>
    /// <para>Action&lt;string&gt; : 发送文本内容。</para>
    /// <para>Action&lt;object&gt; : 会自动将对象解析为Json字符串，发送文本内容。</para>
    /// <para>Action&lt;dynamic&gt; : 会自动将对象解析为Json字符串，发送文本内容。</para>
    /// <para>Func&lt;string,Task&gt; : 异步发送文本内容。</para>
    /// <para>Func&lt;object,Task&gt; : 会自动将对象解析为Json字符串，异步发送文本内容。</para>
    /// <para>Func&lt;dynamic,Task&gt; : 会自动将对象解析为Json字符串，异步发送文本内容。</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class WsMethodAttribute : Attribute
    {
        /// <summary>
        /// 描述Json业务字段，如果不设置，将默认使用方法名称。
        /// </summary>
        public string ThemeValue = string.Empty;
        /// <summary>
        /// <para>标记方法执行完成后是否需要将结果发送。</para>
        /// <para>注意以下返回值，返回的 json 中将不会新建 DataKey 字段：</para>
        /// <para>1.返回类型为 void </para>
        /// <para>2.返回类型为 Task </para>
        /// <para>2.返回类型为 Unit </para>
        /// <para>补充：如果返回类型是Task&lt;TResult&gt;</para>
        /// <para>会进行异步等待，当Task结束后，自动获取TResult进行发送（请避免Task&lt;Task&lt;TResult&gt;&gt;诸如此类的Task泛型嵌套）</para>
        /// </summary>
        public bool IsReturnValue = true;
    }




}
