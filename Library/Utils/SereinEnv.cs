using Serein.Library.Api;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    public static class SereinEnv
    {
        private static IFlowEnvironment environment;

        /// <summary>
        /// 记录全局数据
        /// </summary>
        public static ConcurrentDictionary<string, object> EnvGlobalData { get; } = new ConcurrentDictionary<string, object>();
        /// <summary>
        /// 清空全局数据
        /// </summary>
        public static void ClearGlobalData()
        {
            foreach (var nodeObj in EnvGlobalData.Values)
            {
                if (nodeObj != null)
                {
                    if (typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) && nodeObj is IDisposable disposable)
                    {
                        disposable?.Dispose();
                    }
                }
                else
                {

                }
            }
            EnvGlobalData.Clear();
        }



        /// <summary>
        /// 设置运行流程
        /// </summary>
        /// <param name="environment"></param>
        public static void SetEnv(IFlowEnvironment environment)
        {
            if (environment != null)
            {
                SereinEnv.environment = environment;
            }
        }

        /// <summary>
        /// 输出内容
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="message">内容</param>
        /// <param name="class">级别</param>
        public static void WriteLine(InfoType type, string message, InfoClass @class = InfoClass.General)
        {
            SereinEnv.environment.WriteLine(type,message,@class);
        }


        


    }

}
