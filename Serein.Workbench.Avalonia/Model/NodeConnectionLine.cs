using Avalonia.Controls;
using Serein.Workbench.Avalonia.Custom.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Model
{

    /// <summary>
    /// 绘制的线
    /// </summary>
    public class NodeConnectionLine
    {
        /// <summary>
        /// 将线条绘制出来（临时线）
        /// </summary>
        /// <param name="canvas">放置画布</param>
        /// <param name="line">线的实体</param>
        public NodeConnectionLine(Canvas canvas, ConnectionLineShape line)
        {
            Canvas = canvas;
            Line = line;
            canvas?.Children.Add(line);
        }


        public Canvas Canvas { get; }
        public ConnectionLineShape Line { get; }

        /// <summary>
        /// 移除线
        /// </summary>
        public void Remove()
        {
            Canvas?.Children.Remove(Line);
        }
    }
}
