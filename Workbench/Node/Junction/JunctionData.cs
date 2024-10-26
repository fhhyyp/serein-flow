using Serein.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace Serein.Workbench.Node.View
{

    #region Model，不科学的全局变量
    public class MyLine
    {
        public MyLine(Canvas canvas, ConnectionLineShape line)
        {
            Canvas = canvas;
            Line = line;
            canvas?.Children.Add(line);
        }

        public Canvas Canvas { get; set; }
        public ConnectionLineShape Line { get; set; }

        public void Remove()
        {
            Canvas?.Children.Remove(Line);
        }
    }

    public class ConnectingData
    {

        /// <summary>
        /// 是否正在创建连线
        /// </summary>
        public bool IsCreateing { get; set; }
        /// <summary>
        /// 起始控制点
        /// </summary>
        public JunctionControlBase StartJunction { get; set; }
        /// <summary>
        /// 当前的控制点
        /// </summary>
        public JunctionControlBase CurrentJunction { get; set; }
        /// <summary>
        /// 开始坐标
        /// </summary>
        public Point StartPoint { get; set; }
        /// <summary>
        /// 线条样式
        /// </summary>
        public MyLine MyLine { get; set; }

        /// <summary>
        /// 线条类别（方法调用）
        /// </summary>
        public ConnectionInvokeType ConnectionInvokeType { get; set; } = ConnectionInvokeType.IsSucceed;
        /// <summary>
        /// 线条类别（参数传递）
        /// </summary>
        public ConnectionArgSourceType ConnectionArgSourceType { get; set; } = ConnectionArgSourceType.GetOtherNodeData;

        /// <summary>
        /// 判断当前连接类型
        /// </summary>
        public JunctionOfConnectionType Type => StartJunction.JunctionType.ToConnectyionType();


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


                if (!StartJunction.MyNode.Equals(CurrentJunction.MyNode)
                    && StartJunction.JunctionType.IsCanConnection(CurrentJunction.JunctionType))
                {
                    return true;
                }
                else
                {
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
                MyLine.Line.UpdateStartPoints(point);
            }
            else
            {
                MyLine.Line.UpdateEndPoints(point);

            }
        }

        /// <summary>
        /// 重置
        /// </summary>
        public void Reset()
        {
            IsCreateing = false;
            StartJunction = null;
            CurrentJunction = null;
            MyLine?.Remove();
            ConnectionInvokeType = ConnectionInvokeType.IsSucceed;
            ConnectionArgSourceType = ConnectionArgSourceType.GetOtherNodeData;
        }



    }

    public static class GlobalJunctionData
    {
        //private static ConnectingData? myGlobalData;
        //private static object _lockObj = new object();

        /// <summary>
        /// 创建节点之间控制点的连接行为
        /// </summary>
        public static ConnectingData MyGlobalConnectingData { get; } = new ConnectingData();

        /// <summary>
        /// 删除连接视觉效果
        /// </summary>
        public static void OK()
        {
            MyGlobalConnectingData.Reset();
        }
    }
    #endregion
}
