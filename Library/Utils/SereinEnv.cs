using Serein.Library.Api;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.Library.Utils
{
    public static class SereinEnv
    {
        private static IFlowEnvironment environment;

        #region 全局数据（暂时使用静态全局变量）
        /// <summary>
        /// 记录全局数据
        /// </summary>
        private static ConcurrentDictionary<string, object> EnvGlobalData { get; } = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// 添加或更新全局数据
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static void AddOrUpdateFlowGlobalData(string name, object data)
        {
            SereinEnv.EnvGlobalData.AddOrUpdate(name, data, (k, o) => data);
        }

        /// <summary>
        /// 更改某个数据的名称
        /// </summary>
        /// <param name="oldName">旧名称</param>
        /// <param name="newName">新名称</param>
        /// <returns></returns>
        public static bool ChangeNameFlowGlobalData(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
            {
                return false;
            }
            // 确保存在，然后尝试移除
            if (SereinEnv.EnvGlobalData.ContainsKey(oldName) 
                && SereinEnv.EnvGlobalData.TryRemove(oldName, out var data))
            {
                SereinEnv.EnvGlobalData.AddOrUpdate(newName, data, (k, o) => data);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 获取全局数据
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static object GetFlowGlobalData(string name)
        {
            if (!string.IsNullOrEmpty(name) && SereinEnv.EnvGlobalData.TryGetValue(name, out var data))
            {
                return data;
            }
            else
            {
                return null;
            }
        }
        /// <summary>
        /// 清空全局数据
        /// </summary>
        /// <returns></returns>
        public static void ClearFlowGlobalData()
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
            return;
        }

        #endregion




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
        
        /// <summary>
        /// 输出异常信息
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="class"></param>
        public static void WriteLine(Exception ex, InfoClass @class = InfoClass.General)
        {
            SereinEnv.environment.WriteLine(InfoType.ERROR, ex.ToString(), @class);
        }


        


    }

}
