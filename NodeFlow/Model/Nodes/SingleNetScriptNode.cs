using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model.Nodes
{
    /// <summary>
    /// 单脚本节点（用于脚本控件）
    /// </summary>

    [FlowDataProperty(ValuePath = NodeValuePath.Node, IsNodeImp = true)]
    public partial class SingleNetScriptNode : NodeModelBase
    {
        /// <summary>
        /// 脚本代码
        /// </summary>
        [DataInfo(IsNotification = true)]
        private string _script = string.Empty;

        /// <summary>
        /// 功能提示
        /// </summary>
        [DataInfo(IsNotification = true)]
        private string _tips = "写一下提示吧";

        /// <summary>
        /// 依赖路径
        /// </summary>
        [DataInfo(IsNotification = true)]
        private List<string> _libraryFilePaths = [];

    }

    public partial class SingleNetScriptNode
    {
        /// <summary>
        /// 表达式节点是基础节点
        /// </summary>
        public override bool IsBase => true;

        /// <summary>
        /// 脚本代码
        /// </summary>
        /// <param name="environment"></param>
        public SingleNetScriptNode(IFlowEnvironment environment) : base(environment)
        {
            this.Env = environment;
        }


        /// <summary>
        /// 导出脚本代码
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public override NodeInfo SaveCustomData(NodeInfo nodeInfo)
        {
            dynamic data = new ExpandoObject();
            data.Script = this.Script ?? "";
            nodeInfo.CustomData = data;
            return nodeInfo;
        }

        /// <summary>
        /// 加载自定义数据
        /// </summary>
        /// <param name="nodeInfo"></param>
        public override void LoadCustomData(NodeInfo nodeInfo)
        {
            this.Script = nodeInfo.CustomData?.Script ?? "";

            // 更新变量名
            //for (int i = 0; i < Math.Min(this.MethodDetails.ParameterDetailss.Length, nodeInfo.ParameterData.Length); i++)
            //{
            //    this.MethodDetails.ParameterDetailss[i].Name = nodeInfo.ParameterData[i].ArgName;
            //}


        }








    }
}
