using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow
{
   /* /// <summary>
    /// 输出文件
    /// </summary>
    public class SereinOutputFileData 
    {
        /// <summary>
        /// 基础
        /// </summary>

        public Basic basic { get; set; }

        /// <summary>
        /// 依赖的DLL
        /// </summary>

        public Library[] library { get; set; }

        /// <summary>
        /// 起始节点GUID
        /// </summary>

        public string startNode { get; set; }

        /// <summary>
        /// 节点信息集合
        /// </summary>

        public NodeInfo[] nodes { get; set; }

        /// <summary>
        /// 区域集合
        /// </summary>

        public Region[] regions { get; set; }

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

        public string name { get; set; }

        /// <summary>
        /// 路径
        /// </summary>

        public string path { get; set; }

        /// <summary>
        /// 提示
        /// </summary>

        public string tips { get; set; }

    }
    /// <summary>
    /// 节点
    /// </summary>
    public class NodeInfo
    {
        /// <summary>
        /// GUID
        /// </summary>

        public string guid { get; set; }

        /// <summary>
        /// 名称
        /// </summary>

        public string name { get; set; }

        /// <summary>
        /// 显示标签
        /// </summary>

        public string label { get; set; }

        /// <summary>
        /// 类型
        /// </summary>

        public string type { get; set; }

        /// <summary>
        /// 于画布中的位置
        /// </summary>

        public Position position { get; set; }

        /// <summary>
        /// 真分支节点GUID
        /// </summary>

        public string[] trueNodes { get; set; }

        /// <summary>
        /// 假分支节点
        /// </summary>

        public string[] falseNodes { get; set; }
        public string[] upstreamNodes { get; set; }



        public Parameterdata[] parameterData { get; set; }

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
        public float x { get; set; }
        public float y { get; set; }
    }


    /// <summary>
    /// 区域
    /// </summary>
    public class Region
    {
        public string guid { get; set; }
        public NodeInfo[] childNodes { get; set; }

    }*/
}
