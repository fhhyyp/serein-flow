using Avalonia.Media;
using Serein.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Extension
{

    /// <summary>
    /// 线条颜色
    /// </summary>
    public static class LineExtension
    {
        /// <summary>
        /// 根据连接类型指定颜色
        /// </summary>
        /// <param name="currentConnectionType"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static SolidColorBrush ToLineColor(this ConnectionInvokeType currentConnectionType)
        {
            return currentConnectionType switch
            {
                ConnectionInvokeType.IsSucceed => new SolidColorBrush(Color.Parse("#04FC10")), // 04FC10 & 027E08
                ConnectionInvokeType.IsFail => new SolidColorBrush(Color.Parse("#F18905")),
                ConnectionInvokeType.IsError => new SolidColorBrush(Color.Parse("#FE1343")),
                ConnectionInvokeType.Upstream => new SolidColorBrush(Color.Parse("#4A82E4")),
                ConnectionInvokeType.None => new SolidColorBrush(Color.Parse("#56CEF6")),
                _ => throw new Exception(),
            };
        }
        /// <summary>
        /// 根据连接类型指定颜色
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static SolidColorBrush ToLineColor(this ConnectionArgSourceType connection)
        {
            return connection switch
            {

                ConnectionArgSourceType.GetPreviousNodeData => new SolidColorBrush(Color.Parse("#56CEF6")), // 04FC10 & 027E08
                ConnectionArgSourceType.GetOtherNodeData => new SolidColorBrush(Color.Parse("#56CEF6")),
                ConnectionArgSourceType.GetOtherNodeDataOfInvoke => new SolidColorBrush(Color.Parse("#56CEF6")),
                _ => throw new Exception(),
            };
        }

    }

}
