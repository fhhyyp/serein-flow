using Serein.Library;
using Serein.Workbench.Node.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Serein.Workbench.Extension
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
                ConnectionInvokeType.IsSucceed => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10")), // 04FC10 & 027E08
                ConnectionInvokeType.IsFail => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F18905")),
                ConnectionInvokeType.IsError => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FE1343")),
                ConnectionInvokeType.Upstream => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A82E4")),
                ConnectionInvokeType.None => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#56CEF6")),
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
                ConnectionArgSourceType.GetPreviousNodeData => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#56CEF6")), // 04FC10 & 027E08
                ConnectionArgSourceType.GetOtherNodeData => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#56CEF6")),
                ConnectionArgSourceType.GetOtherNodeDataOfInvoke => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B06BBB")),
                _ => throw new Exception(),
            };
        }

    }
}
