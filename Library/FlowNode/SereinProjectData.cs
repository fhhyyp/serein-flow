using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{

    /// <summary>
    /// 环境信息
    /// </summary>
    public class FlowEnvInfo
    {
        /// <summary>
        /// 环境方法信息
        /// </summary>
        public LibraryMds[] LibraryMds { get; set; }
        /// <summary>
        /// 项目信息
        /// </summary>
        public SereinProjectData Project { get; set; }

        // IOC节点对象信息
    }

    /// <summary>
    /// 程序集相关的方法信息
    /// </summary>
    public class LibraryMds
    {
        /// <summary>
        /// 程序集名称
        /// </summary>
        public string AssemblyName { get; set; }
        /// <summary>
        /// 相关的方法详情
        /// </summary>
        public MethodDetailsInfo[] Mds { get; set; }

    }




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
    /// 基础，项目文件相关
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
    /// 画布信息，项目文件相关
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
        public double Height { get; set; }

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
    /// 项目依赖的程序集，项目文件相关
    /// </summary>
    public class Library
    {
        /// <summary>
        /// 文件名称
        /// </summary>

        public string FileName { get; set; }

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 程序集名称
        /// </summary>
        public string AssemblyName { get; set; }
    }

    /// <summary>
    /// 节点信息，项目文件相关
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
        public ParameterData[] ParameterData { get; set; }

        /// <summary>
        /// 如果是区域控件，则会存在子项。
        /// </summary>
        public string[] ChildNodeGuids { get; set; }

        /// <summary>
        /// 于画布中的位置
        /// </summary>

        public PositionOfUI Position { get; set; }

        /// <summary>
        /// 是否选中（暂时无效）
        /// </summary>
        public bool IsSelect { get; set; }
    }

    /// <summary>
    /// 参数信息，项目文件相关
    /// </summary>
    public class ParameterData
    {
        /// <summary>
        /// 参数类型，true时使用自定义的入参，false时由运行环境自动传参
        /// </summary>
        public bool State { get; set; }

        /// <summary>
        /// 参数来源节点
        /// </summary>
        public string SourceNodeGuid { get; set; }

        /// <summary>
        /// 来源类型
        /// </summary>
        public string SourceType { get; set; }

        /// <summary>
        /// 自定义入参
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// 表达式相关节点的表达式内容
        /// </summary>
        // public string Expression { get; set; }

    }


    /// <summary>
    /// 节点于画布中的位置（通用类）
    /// </summary>
    [NodeProperty]
    public partial class PositionOfUI
    {
        /// <summary>
        /// 构造一个坐标
        /// </summary>
        public PositionOfUI(double x, double y)
        {
            _x = x; _y = y;
        }

        /// <summary>
        /// 指示控件在画布的横向向方向上的位置
        /// </summary>
        [PropertyInfo]
        private double _x = 0;

        /// <summary>
        /// 指示控件在画布的纵向方向上的位置
        /// </summary>
        [PropertyInfo]
        private double _y = 0;
    }



}
