using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Entity
{

    /// <summary>
    /// 项目保存文件
    /// </summary>
    public class SereinProjectData
    {
        /// <summary>
        /// 基础
        /// </summary>

        public Basic Basic { get; set; }

        /// <summary>
        /// 依赖的DLL
        /// </summary>

        public Library[] Librarys { get; set; }

        /// <summary>
        /// 起始节点GUID
        /// </summary>

        public string StartNode { get; set; }

        /// <summary>
        /// 节点集合
        /// </summary>

        public NodeInfo[] Nodes { get; set; }


    }

    /// <summary>
    /// 基础
    /// </summary>
    public class Basic
    {
        /// <summary>
        /// 画布
        /// </summary>

        public FlowCanvas Canvas { get; set; }

        /// <summary>
        /// 版本
        /// </summary>

        public string Versions { get; set; }
    }
    /// <summary>
    /// 画布
    /// </summary>
    public class FlowCanvas
    {
        /// <summary>
        /// 宽度
        /// </summary>
        public double Width { get; set; }
        /// <summary>
        /// 高度
        /// </summary>
        public double Lenght { get; set; }

        /// <summary>
        /// 预览位置X
        /// </summary>
        public double ViewX { get; set; }

        /// <summary>
        /// 预览位置Y
        /// </summary>
        public double ViewY { get; set; }

        /// <summary>
        /// 缩放比例X
        /// </summary>
        public double ScaleX { get; set; }

        /// <summary>
        /// 缩放比例Y
        /// </summary>
        public double ScaleY { get; set; }
    }

    /// <summary>
    /// DLL
    /// </summary>
    public class Library
    {
        /// <summary>
        /// DLL名称
        /// </summary>

        public string Name { get; set; }

        /// <summary>
        /// 路径
        /// </summary>

        public string Path { get; set; }


    }
    /// <summary>
    /// 节点
    /// </summary>
    public class NodeInfo
    {
        /// <summary>
        /// GUID
        /// </summary>

        public string Guid { get; set; }

        /// <summary>
        /// 名称
        /// </summary>

        public string MethodName { get; set; }

        /// <summary>
        /// 显示标签
        /// </summary>

        public string Label { get; set; }

        /// <summary>
        /// 类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 真分支节点GUID
        /// </summary>

        public string[] TrueNodes { get; set; }

        /// <summary>
        /// 假分支节点
        /// </summary>

        public string[] FalseNodes { get; set; }
        /// <summary>
        /// 上游分支
        /// </summary>
        public string[] UpstreamNodes { get; set; }
        /// <summary>
        /// 异常分支
        /// </summary>
        public string[] ErrorNodes { get; set; }

        /// <summary>
        /// 参数
        /// </summary>
        public Parameterdata[] ParameterData { get; set; }

        /// <summary>
        /// 如果是区域控件，则会存在子项。
        /// </summary>
        public string[] ChildNodeGuids { get; set; }


        /// <summary>
        /// 于画布中的位置
        /// </summary>

        public Position Position { get; set; }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelect { get; set; }
    }

    /// <summary>
    /// 显示参数
    /// </summary>
    public class Parameterdata
    {
        public bool State { get; set; }
        public string Value { get; set; }
        public string Expression { get; set; }

    }


    /// <summary>
    /// 节点于画布中的位置
    /// </summary>
    public class Position
    {
        public Position(double x, double y)
        {
            this.X = x; this.Y = y;
        }

        public double X { get; set; } = 0;
        public double Y { get; set; } = 0;
    }



}
