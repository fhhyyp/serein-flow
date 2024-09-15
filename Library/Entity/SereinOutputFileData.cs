using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Entity
{

    /// <summary>
    /// 输出文件
    /// </summary>
    public class SereinOutputFileData
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

        ///// <summary>
        ///// 区域集合
        ///// </summary>

        //public Region[] Regions { get; set; }

    }

    /// <summary>
    /// 基础
    /// </summary>
    public class Basic
    {
        /// <summary>
        /// 画布
        /// </summary>

        public FlowCanvas canvas { get; set; }

        /// <summary>
        /// 版本
        /// </summary>

        public string versions { get; set; }

        // 预览位置

        // 缩放比例
    }
    /// <summary>
    /// 画布
    /// </summary>
    public class FlowCanvas
    {
        /// <summary>
        /// 宽度
        /// </summary>
        public float width { get; set; }
        /// <summary>
        /// 高度
        /// </summary>
        public float lenght { get; set; }
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
        public NodeInfo[] ChildNodes { get; set; }


        /// <summary>
        /// 于画布中的位置
        /// </summary>

        public Position Position { get; set; }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelect { get; set; }
    }

    public class Parameterdata
    {
        public bool state { get; set; }
        public string value { get; set; }
        public string expression { get; set; }

    }


    /// <summary>
    /// 节点于画布中的位置
    /// </summary>
    public class Position
    {
        public float X { get; set; }
        public float Y { get; set; }
    }


    /// <summary>
    /// 区域
    /// </summary>
    public class Region
    {
        public string guid { get; set; }
        public NodeInfo[] ChildNodes { get; set; }

    }
}
