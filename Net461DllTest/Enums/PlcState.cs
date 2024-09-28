using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net461DllTest.Enums
{
    public enum PlcState
    {
        /// <summary>
        /// 关机
        /// </summary>
        PowerOff,
        /// <summary>
        /// 正在运行
        /// </summary>
        Runing,
        /// <summary>
        /// 发生异常
        /// </summary>
        Error,
        /// <summary>
        /// 维护中
        /// </summary>
        Maintenance,
    }
}
