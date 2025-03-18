using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model
{

    [NodeProperty(ValuePath = NodeValuePath.Node)]
    public partial class SingleNetScriptNode : NodeModelBase
    {
        /// <summary>
        /// 脚本代码
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private string _script;

        /// <summary>
        /// 功能提示
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private string _tips = "写一下提示吧";

        /// <summary>
        /// 依赖路径
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private List<string> _libraryFilePaths;

    }

    public partial class SingleNetScriptNode
    {
        /// <summary>
        /// 表达式节点是基础节点
        /// </summary>
        public override bool IsBase => true;

        public SingleNetScriptNode(IFlowEnvironment environment) : base(environment)
        {
            this.Env = environment;
        }





        public override void OnCreating()
        {
            //MethodInfo? method = this.GetType().GetMethod(nameof(GetFlowApi));
            //if (method != null)
            //{
            //    ScriptInterpreter.AddFunction(nameof(GetFlowApi), method, () => this); // 挂载获取流程接口
            //}

            //var md = MethodDetails;
            //var pd = md.ParameterDetailss ??= new ParameterDetails[1];
            //md.ParamsArgIndex = 0;
            //pd[0] = new ParameterDetails
            //{
            //    Index = 0,
            //    Name = "object",
            //    IsExplicitData = true,
            //    DataValue = string.Empty,
            //    DataType = typeof(object),
            //    ExplicitType = typeof(object),
            //    ArgDataSourceNodeGuid = string.Empty,
            //    ArgDataSourceType = ConnectionArgSourceType.GetPreviousNodeData,
            //    NodeModel = this,
            //    InputType = ParameterValueInputType.Input,
            //    Items = null,
            //    IsParams = true,
            //    Description = "脚本节点入参"

            //};

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
