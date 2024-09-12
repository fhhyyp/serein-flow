using System;

namespace Serein.Library.Core.Http
{
    /// <summary>
    /// 表示参数为url中的数据（Get请求中不需要显式标注）
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class IsUrlDataAttribute : Attribute
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
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class IsBobyDataAttribute : Attribute
    {

    }

    /// <summary>
    /// 表示该控制器会被自动注册（与程序集同一命名空间，暂时不支持运行时自动加载DLL，需要手动注册）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AutoHostingAttribute(string url = "") : Attribute
    {
        public string Url { get; } = url;
    }
    /// <summary>
    /// 表示该属性为自动注入依赖项
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class AutoInjectionAttribute : Attribute
    {
    }
    

    /// <summary>
    /// 方法的接口类型与附加URL
    /// </summary>
    /// <remarks>
    ///  假设UserController.Add()的WebAPI特性中
    ///  http是HTTP.POST
    ///  url被显示标明“temp”
    ///  那么请求的接口是POST,URL是
    ///  [http://localhost:8080]/user/add/temp
    /// </remarks>
    /// <param name="http"></param>
    /// <param name="url"></param>
    [AttributeUsage(AttributeTargets.Method)]

    public sealed class WebApiAttribute() : Attribute

    {
        public API Type ;
        public string Url ;
        /// <summary>
        /// 方法名称不作为url的部分
        /// </summary>
        public bool IsUrl;
    }
    [AttributeUsage(AttributeTargets.Method)]

    public sealed class ApiPostAttribute() : Attribute

    {
        public string Url;
        /// <summary>
        /// 方法名称不作为url的部分
        /// </summary>
        public bool IsUrl = true;
    }
    [AttributeUsage(AttributeTargets.Method)]

    public sealed class ApiGetAttribute() : Attribute

    {
        public string Url;
        /// <summary>
        /// 方法名称不作为url的部分
        /// </summary>
        public bool IsUrl = true;
    }

    /*public sealed class WebApiAttribute(API http, bool isUrl = true, string url = "") : Attribute
    {
        public API Http { get; } = http;
        public string Url { get; } = url;
        /// <summary>
        /// 方法名称不作为url的部分
        /// </summary>
        public bool IsUrl { get; } = isUrl;
    }*/
    public enum API
    {
        POST,
        GET,
        //PUT,
        //DELETE
    }
}
