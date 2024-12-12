using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model
{

    
        /// <summary>
    /// Expression Operation - 表达式操作
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.Node)]
    public partial class SingleGlobalDataNode : NodeModelBase
    {
        /// <summary>
        /// 表达式
        /// </summary>
        [PropertyInfo(IsNotification = true, CustomCodeAtStart = "ChangeName(value);")] 
        private string _keyName;

    }
     
    /// <summary>
    /// 全局数据节点
    /// </summary>
    public partial class SingleGlobalDataNode : NodeModelBase
    {
        public SingleGlobalDataNode(IFlowEnvironment environment) : base(environment)
        {
        }

        /// <summary>
        /// 数据节点
        /// </summary>
        private string? DataNodeGuid;

        /// <summary>
        /// 设置数据节点
        /// </summary>
        /// <param name="dataNode"></param>
        public void SetDataNode(NodeModelBase dataNode)
        {
            DataNodeGuid = dataNode.Guid;
        }

        private void ChangeName(string newName)
        {
            if(SereinEnv.GetFlowGlobalData(_keyName) == null)
            {
                return;
            }
            SereinEnv.ChangeNameFlowGlobalData(_keyName, newName);
        }

        /// <summary>
        /// 设置全局数据
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<object> ExecutingAsync(IDynamicContext context)
        {
            if (string.IsNullOrEmpty(KeyName))
            {
                context.NextOrientation = ConnectionInvokeType.IsError;
                SereinEnv.WriteLine(InfoType.ERROR, $"全局数据的KeyName不能为空[{this.Guid}]");
                return null;
            }
            if (DataNodeGuid == null)
            {
                context.NextOrientation = ConnectionInvokeType.IsError;
                SereinEnv.WriteLine(InfoType.ERROR, $"全局数据节点没有数据[{this.Guid}]");
                return null;
            }

            try
            {
                var result = await context.Env.InvokeNodeAsync(context, DataNodeGuid);
                SereinEnv.AddOrUpdateFlowGlobalData(KeyName, result);
                return result;
            }
            catch (Exception ex)
            {
                context.NextOrientation = ConnectionInvokeType.IsError;
                context.ExceptionOfRuning = ex;
                return null;
            }
        }
        
        /// <summary>
        /// 保存全局变量的数据
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public override NodeInfo SaveCustomData(NodeInfo nodeInfo)
        {
            dynamic data = new ExpandoObject();
            nodeInfo.CustomData = data;

            data.KeyName = KeyName; // 变量名称

            if (string.IsNullOrEmpty(DataNodeGuid))
            {
                return nodeInfo;
            }
            
            data.DataNodeGuid = DataNodeGuid; // 数据节点Guid
            
            nodeInfo.ChildNodeGuids = [DataNodeGuid];
            return nodeInfo;
        }

        /// <summary>
        /// 加载全局变量的数据
        /// </summary>
        /// <param name="nodeInfo"></param>
        public override void LoadCustomData(NodeInfo nodeInfo)
        {
            KeyName = nodeInfo.CustomData?.KeyName;
            DataNodeGuid = nodeInfo.CustomData?.DataNodeGuid;
        }

        /// <summary>
        /// 需要移除数据节点
        /// </summary>
        public override void Remove()
        {
            // 移除数据节点
            _ = this.Env.RemoveNodeAsync(DataNodeGuid);
        }

    }
}
