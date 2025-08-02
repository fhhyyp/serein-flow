namespace Serein.Proto.WebSocket.Attributes
{
    /// <summary>
    /// <para>标记该类是处理模板</para>
    /// <para>处理模板需要继承 ISocketHandleModule 接口，否则接受到 WebSocket 数据时，将无法进行调用相应的处理模板。</para> 
    /// <para>使用方式：</para>
    /// <para>[AutoSocketModule(ThemeKey = "theme", DataKey = "data")]</para>
    /// <para>public class PlcSocketService : ISocketHandleModule</para>
    /// <para>类中方法示例：void AddUser(string name,int age)</para>
    /// <para>Json示例：{ "theme":"AddUser", //【ThemeKey】 </para>
    /// <para>  "data": { // 【DataKey】  </para>              
    /// <para>    "name":"张三",         </para>
    /// <para>    "age":35,   } }       </para>
    /// <para>WebSocket中收到以上该Json时，通过ThemeKey获取到"AddUser"，然后找到AddUser()方法</para>
    /// <para>然后根据方法入参名称，从data对应的json数据中取出"name""age"对应的数据作为入参进行调用。AddUser("张三",35)</para>
    /// <para></para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class WebSocketModuleAttribute : Attribute
    {
        /// <summary>
        /// 业务标识
        /// </summary>
        public string ThemeKey = string.Empty;
        /// <summary>
        /// 数据标识
        /// </summary>
        public string DataKey = string.Empty;
        /// <summary>
        /// ID标识
        /// </summary>
        public string MsgIdKey = string.Empty;

        /// <summary>
        /// 指示应答数据回复方法返回值
        /// </summary>
        public bool IsResponseUseReturn = true;
    }




}
