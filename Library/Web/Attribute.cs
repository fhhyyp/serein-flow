using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Web
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
    public class BobyAttribute : Attribute
    {

    }
    /// <summary>
    /// 自动注册控制器
    /// </summary>
    public class AutoHostingAttribute : Attribute
    {
        public string Url { get; }
        public AutoHostingAttribute(string url = "")
        {
            this.Url = url;
        }
    }

    /// <summary>
    /// 方法的接口类型与附加URL
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class WebApiAttribute : Attribute
    {
        public API Http; // HTTP 请求类型
        public string Url; // URL 路径
        /// <summary>
        /// 方法名称不作为url的部分
        /// </summary>
        public bool IsUrl;


        /// <summary>
        ///  假设UserController.Add()的WebAPI特性中
        ///  http是HTTP.POST
        ///  url被显示标明“temp”
        ///  那么请求的接口是POST,URL是
        ///  [http://localhost:8080]/user/add/temp
        /// </summary>
        /// <param name="http"></param>
        /// <param name="url"></param>
        public WebApiAttribute(API http = API.POST, bool isUrl = true, string url = "")
        {
            Http = http;
            Url = url;
            IsUrl = isUrl;
        }
    }

    public enum API
    {
        POST,
        GET,
        //PUT,
        //DELETE
    }
}
