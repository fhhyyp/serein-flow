using System;

namespace Serein.Proto.HttpApi
{
    /// <summary>
    /// Web Api 控制器基类
    /// </summary>
    public class ControllerBase
    {
        /// <summary>
        /// 请求的url
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// 请求的body数据
        /// </summary>
        public string? Body { get; set; }

        /// <summary>
        /// 获取日志信息
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public string GetLog(Exception ex)
        {
            return "Url : " + Url + Environment.NewLine +
                   "Ex : " + ex + Environment.NewLine +
                   "Data : " + Body + Environment.NewLine;
        }
    }
}
