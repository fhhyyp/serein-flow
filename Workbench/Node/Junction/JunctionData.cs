using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace Serein.Workbench.Node.View
{

    #region Model，不科学的全局变量
    public class MyLine
    {
        public MyLine(Canvas canvas, BezierLine line)
        {
            Canvas = canvas;
            VirtualLine = line;
            canvas?.Children.Add(line);
        }

        public Canvas Canvas { get; set; }
        public BezierLine VirtualLine { get; set; }

        public void Remove()
        {
            Canvas?.Children.Remove(VirtualLine);
        }
    }

    public class ConnectingData
    {
        public JunctionControlBase StartJunction { get; set; }
        public JunctionControlBase CurrentJunction { get; set; }
        public Point StartPoint { get; set; }
        public MyLine VirtualLine { get; set; }

        /// <summary>
        /// 是否允许连接
        /// </summary>

        public bool IsCanConnected { get
            {
                if(StartJunction is null
                    || CurrentJunction is null
                    )
                {
                    return false;
                }
                if (!StartPoint.Equals(CurrentJunction))
                {
                    return true;
                }
                else
                {
                    // 自己连接自己的情况下，只能是从arg控制点连接到execute控制点。
                    if (CurrentJunction.JunctionType == Library.JunctionType.Execute
                        && StartJunction.JunctionType == Library.JunctionType.ArgData)
                    {

                        return true;
                    }

                    if (CurrentJunction.JunctionType == Library.JunctionType.ArgData
                        && StartJunction.JunctionType == Library.JunctionType.Execute)
                    {
                        // 需要是自己连接自己，且只能是从arg控制点连接到execute控制点。
                        return true;
                    }
                    return false;
                }
            } 
        }

        /// <summary>
        /// 更新临时的连接线
        /// </summary>
        /// <param name="point"></param>
        public void UpdatePoint(Point point)
        {
            if (StartJunction is null
                    || CurrentJunction is null
                    )
            {
                return;
            }
            if (StartJunction.JunctionType == Library.JunctionType.Execute 
                || StartJunction.JunctionType == Library.JunctionType.ArgData)
            {
                VirtualLine.VirtualLine.UpdateStartPoints(point);
            }
            else
            {
                VirtualLine.VirtualLine.UpdateEndPoints(point);

            }
        }



    }

    public static class GlobalJunctionData
    {
        private static ConnectingData? myGlobalData;
        private static object _lockObj = new object();

        /// <summary>
        /// 创建节点之间控制点的连接行为
        /// </summary>
        public static ConnectingData? MyGlobalConnectingData
        {
            get => myGlobalData;
            set
            {
                lock (_lockObj)
                {
                    myGlobalData ??= value;
                }
            }
        }

        /// <summary>
        /// 删除连接视觉效果
        /// </summary>
        public static void OK()
        {
            myGlobalData?.VirtualLine.Remove();
            myGlobalData = null;
        }
    }
    #endregion
}
