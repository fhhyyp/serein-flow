using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.WebSockets;

namespace Serein.Library.Network.WebSocketCommunication
{
    /// <summary>
    /// <para>标记该类是处理模板，需要获取WebSocketServer/WebSocketClient了实例后，使用(Server/Client).MsgHandleHelper.AddModule()进行添加。</para>
    /// <para>处理模板需要继承 ISocketHandleModule 接口，否则WebSocket接受到数据时，将无法进行调用相应的处理模板。</para> 
    /// <para>使用方式：</para>
    /// <para>[AutoSocketModule(ThemeKey = "theme", DataKey = "data")]</para>
    /// <para>public class PlcSocketService : ISocketHandleModule</para>
    /// <para>类中方法示例：void AddUser(string name,age 35)</para>
    /// <para>Json示例：{ "theme":"AddUser", //【ThemeKey】 </para>
    /// <para>  "data": { // 【DataKey】  </para>              
    /// <para>    "name":"张三",         </para>
    /// <para>    "age":35,   } }       </para>
    /// <para>WebSocket中收到以上该Json时，通过ThemeKey获取到"AddUser"，然后找到AddUser()方法，并将"data"作为数据对象，取出相应数据作为入参(args:"张三",35)进行调用(如果有)</para>
    /// <para></para>
    /// 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AutoSocketModuleAttribute : Attribute
    {
        public string ThemeKey;
        public string DataKey;
    }


    /// <summary>
    /// <para>作用：WebSocket中处理Json时，将通过Json中ThemeKey 对应的内容（ThemeValue）自动路由到相应方法进行处理。</para>
    /// <para>如果没有显式设置ThemeValue，将默认使用方法名称作为ThemeValue。</para>
    /// <para>如果没有显式设置IsReturnValue标记为false，当方法顺利完成（没有抛出异常，且返回对象非null），会自动转为json文本发送回去</para>
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
    public sealed class AutoSocketHandleAttribute : Attribute
    {
        /// <summary>
        /// 描述Json业务字段，如果不设置，将默认使用方法名称。
        /// </summary>
        public string ThemeValue = string.Empty;
        /// <summary>
        /// <para>标记方法执行完成后是否需要将结果发送。</para>
        /// <para>但以下情况将不会发送：</para>
        /// <para>1.返回类型为void</para>
        /// <para>2.返回类型为Task</para>
        /// <para>3.返回了null</para>
        /// <para>补充：如果返回类型是Task&lt;TResult&gt;</para>
        /// <para>会进行异步等待，当Task结束后，自动获取TResult进行发送（请避免Task&lt;Task&lt;TResult&gt;&gt;诸如此类的Task泛型嵌套）</para>
        /// </summary>
        public bool IsReturnValue = true;
    }
   
    internal class SocketHandleModel
    {
        public string ThemeValue { get; set; } = string.Empty;
        public bool IsReturnValue { get; set; } = true;
    }




}
