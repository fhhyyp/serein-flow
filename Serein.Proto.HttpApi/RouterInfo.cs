using System.Reflection;

namespace Serein.Proto.HttpApi
{
    /// <summary>
    /// 路由信息
    /// </summary>
    public class RouterInfo
    {
#if NET6_0_OR_GREATER
        /// <summary>
        /// 接口类型
        /// </summary>
        public ApiType ApiType { get; set; }
        /// <summary>
        /// 接口URL
        /// </summary>
        public required string Url { get; set; }
        /// <summary>
        /// 对应的处理方法
        /// </summary>
        public required MethodInfo MethodInfo { get; set; }
#else
  /// <summary>
        /// 接口类型
        /// </summary>
        public ApiType ApiType { get; set; }
        /// <summary>
        /// 接口URL
        /// </summary>
        public string Url { get; set; }
        /// <summary>
        /// 对应的处理方法
        /// </summary>
        public MethodInfo MethodInfo { get; set; }  
#endif
    }


    
}

