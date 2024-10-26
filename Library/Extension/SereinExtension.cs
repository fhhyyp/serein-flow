using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 拓展方法
    /// </summary>
    public static partial class SereinExtension
    {
        /// <summary>
        /// 判断连接类型
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public static JunctionOfConnectionType ToConnectyionType(this JunctionType start)
        {
            if (start == JunctionType.Execute
                    || start == JunctionType.NextStep)
            {
                return JunctionOfConnectionType.Invoke;
            }
            else
            {
                return JunctionOfConnectionType.Arg;
            }
        }
        /// <summary>
        /// 判断是否运行连接
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static bool IsCanConnection(this JunctionType start,JunctionType end)
        {
            if(start == end)
            {
                return false;
            }

            var startType = start.ToConnectyionType();
            if (startType == JunctionOfConnectionType.Invoke)
            {
                return (end == JunctionType.Execute && start == JunctionType.NextStep)
                    || (start == JunctionType.Execute && end == JunctionType.NextStep);
            }
            else // if (startType == JunctionOfConnectionType.Arg)
            {
                return (end == JunctionType.ArgData && start == JunctionType.ReturnData)
                    || (start == JunctionType.ArgData && end == JunctionType.ReturnData);
            }

            //var endType = end.ToConnectyionType();
            //if (startType != endType
            //    || startType == JunctionOfConnectionType.None
            //    || endType == JunctionOfConnectionType.None)
            //{
            //    return false;
            //}
            //else
            //{
            //    if (startType == JunctionOfConnectionType.Invoke)
            //    {

            //        return end == JunctionType.NextStep;
            //    }
            //    else // if (startType == JunctionOfConnectionType.Arg)
            //    {
            //        return end == JunctionType.ReturnData;
            //    }
            //}
        }

    }
}
