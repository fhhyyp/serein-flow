using System.Collections.Generic;
using System.Reflection;

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
        public FlowLibraryInfo[] LibraryMds { get; set; }
        /// <summary>
        /// 项目信息
        /// </summary>
        public SereinProjectData Project { get; set; }

    }



    /// <summary>
    /// 程序集相关的方法信息
    /// </summary>
    public class FlowLibraryInfo
    {
        /// <summary>
        /// 程序集名称
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 相关的方法详情
        /// </summary>
        public List<MethodDetailsInfo> MethodInfos { get; set; }

    }




    /// <summary>
    /// 项目数据
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

        public FlowLibraryInfo[] Librarys { get; set; }

        /// <summary>
        /// 画布集合
        /// </summary>
        public FlowCanvasDetailsInfo[] Canvass { get; set; }

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
        ///// <summary>
        ///// 画布
        ///// </summary>
        //public FlowCanvasInfo Canvas { get; set; }

        /// <summary>
        /// 版本
        /// </summary>

        public string Versions { get; set; }
    }


/*   
    /// <summary>
    /// 项目依赖的程序集，项目文件相关
    /// </summary>
    /// <summary>
    public class FlowLibraryInfo
    {

    }
*/
   
    /// <summary>
    /// 节点信息，项目文件相关
    /// </summary>
    public class NodeInfo
    {
        /// <summary>
        /// 所属画布Guid
        /// </summary>
        public string CanvasGuid { get; set; }  

        /// <summary>
        /// 节点的GUID
        /// </summary>
        public string Guid { get; set; }

        /// <summary>
        /// 是否全局公开
        /// </summary>
        public bool IsPublic { get; set; } 

        /// <summary>
        /// 节点方法所属的程序集名称
        /// </summary>
        public string AssemblyName { get;set; }

        /// <summary>
        /// 节点对应的名称
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
        /// 父节点集合
        /// </summary>
        public Dictionary<ConnectionInvokeType, string[]> PreviousNodes { get; set; }

        /// <summary>
        /// 后续节点集合
        /// </summary>
        public Dictionary<ConnectionInvokeType, string[]> SuccessorNodes { get; set; }

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
        /// 如果节点放置在了区域控件上，这里会有父级节点Guid
        /// </summary>
        public string ParentNodeGuid{ get; set; }


        /// <summary>
        /// 如果是区域控件，则会存在子项，这里记录的是子项的Guid。
        /// </summary>
        public string[] ChildNodeGuids { get; set; }

        /// <summary>
        /// 于画布中的位置
        /// </summary>

        public PositionOfUI Position { get; set; }


        /// <summary>
        /// 是否中断
        /// </summary>
        public bool IsInterrupt { get; set; }

        /// <summary>
        /// 是否使能
        /// </summary>
        public bool IsEnable { get; set; }

        /// <summary>
        /// 是否保护参数
        /// </summary>
        public bool IsProtectionParameter { get; set; }



        /// <summary>
        /// 自定义数据
        /// </summary>
        public dynamic CustomData { get; set; }
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
        /// 参数名称
        /// </summary>
        public string ArgName { get; set; }

        /// <summary>
        /// 自定义入参
        /// </summary>
        public string Value { get; set; }

    }


    /// <summary>
    /// 节点于画布中的位置（通用类）
    /// </summary>
    [FlowDataProperty]
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
        [DataInfo]
        private double _x = 0;

        /// <summary>
        /// 指示控件在画布的纵向方向上的位置
        /// </summary>
        [DataInfo]
        private double _y = 0;
    }



}
