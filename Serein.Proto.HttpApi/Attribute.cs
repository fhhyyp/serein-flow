using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Proto.HttpApi
{
    /// <summary>
    /// 表示参数为url中的数据
    /// </summary>
    public class UrlAttribute : Attribute
    {

    }
    /// <summary>
    /// 表示入参参数为整个boby的数据
    /// <para>
    /// 例如：User类型含有int id、string name字段</para>
    /// <para>
    /// ① Add(User user)</para>
    ///  <para>请求需要传入的json为
    ///      {"user":{
    ///        "id":2,
    ///        "name":"李志忠"}}</para>
    ///   <para>
    /// ② Add([Boby]User user)</para>
    ///  <para>请求需要传入的json为
    ///      {"id":2,"name":"李志忠"}</para>
    /// 
    /// </summary>
    public class BodyAttribute : Attribute
    {

    }
    /// <summary>
    /// 标记该类为 Web Api 处理类
    /// </summary>
    public class WebApiControllerAttribute : Attribute
    {
        /// <summary>
        ///  URL 路径
        /// </summary>
        public string Url { get; }
        public WebApiControllerAttribute(string url = "")
        {
            Url = url;
        }
    }

    /// <summary>
    /// 方法的接口类型与附加URL
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class WebApiAttribute : Attribute
    {
        /// <summary>
        ///  HTTP 请求类型
        /// </summary>
        public ApiType ApiType;
        /// <summary>
        ///  URL 路径
        /// </summary>
        public string Url = string.Empty; 

        public WebApiAttribute(ApiType http = ApiType.POST, string url = "")
        {
            ApiType = http;
            Url = url;
        }
    }

    public enum ApiType
    {
        POST,
        GET,
        //PUT,
        //DELETE
    }
}
