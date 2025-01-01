using Avalonia;
using Serein.Library;
using Serein.Workbench.Avalonia.Custom.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Model
{

    /// <summary>
    /// 节点之间连接线的相关控制方法
    /// </summary>
    public class ConnectingData
    {
        /// <summary>
        /// 是否正在创建连线
        /// </summary>
        public bool IsCreateing { get; set; }
        /// <summary>
        /// 起始控制点
        /// </summary>
        public NodeJunctionView? StartJunction { get; set; }
        /// <summary>
        /// 当前的控制点
        /// </summary>
        public NodeJunctionView? CurrentJunction { get; set; }
        /// <summary>
        /// 开始坐标
        /// </summary>
        public Point StartPoint { get; set; }
        /// <summary>
        /// 线条样式
        /// </summary>
        public MyLine? TempLine { get; set; }

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
        public JunctionOfConnectionType? Type => StartJunction?.JunctionType.ToConnectyionType();


        /// <summary>
        /// 是否允许连接
        /// </summary>

        public bool IsCanConnected
        {
            get
            {

                if (StartJunction is null
                    || CurrentJunction is null
                    )
                {
                    return false;
                }
                if(StartJunction?.MyNode is null)
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
                TempLine?.Line.UpdateStartPoints(point);
            }
            else
            {
                TempLine?.Line.UpdateEndPoints(point);

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
            TempLine?.Remove();
            ConnectionInvokeType = ConnectionInvokeType.IsSucceed;
            ConnectionArgSourceType = ConnectionArgSourceType.GetOtherNodeData;
        }



    }
}
