﻿using Newtonsoft.Json.Linq;
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
        [PropertyInfo(IsNotification = true, CustomCodeAtStart = "// ChangeName(value);")] 
        private string _keyName;

    }
     
    /// <summary>
    /// 全局数据节点
    /// </summary>
    public partial class SingleGlobalDataNode : NodeModelBase, INodeContainer
    {
        /// <summary>
        /// 全局数据节点是基础节点
        /// </summary>
        public override bool IsBase => true;
        /// <summary>
        /// 数据源只允许放置1个节点。
        /// </summary>
        public override int MaxChildrenCount => 1;

        public SingleGlobalDataNode(IFlowEnvironment environment) : base(environment)
        {
        }

        /// <summary>
        /// 数据来源的节点
        /// </summary>
        private NodeModelBase? DataNode;

        /// <summary>
        /// 有节点被放置
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <returns></returns>
        public bool PlaceNode(NodeModelBase nodeModel)
        {
            if(DataNode is null)
            {
                // 放置节点
                nodeModel.ContainerNode = this;  
                ChildrenNode.Add(nodeModel);
                DataNode = nodeModel;
                return true;
            }
            else
            {
                // 全局数据节点只有一个子控件
                return false;
            }

            
        }


        public bool TakeOutNode(NodeModelBase nodeModel)
        {
            if (ChildrenNode.Contains(nodeModel))
            {
                ChildrenNode.Remove(nodeModel);
                nodeModel.ContainerNode = null;
                DataNode = null;
                return true;
            }
            else
            {
                return false;
            }
            
        }

        public async void TakeOutAll()
        {
            foreach (var nodeModel in ChildrenNode) 
            {
                await nodeModel.Env.TakeOutNodeToContainerAsync(nodeModel.Guid);
            }
            DataNode = null;
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
            if (DataNode is null)
            {
                context.NextOrientation = ConnectionInvokeType.IsError;
                SereinEnv.WriteLine(InfoType.ERROR, $"全局数据节点没有设置数据来源[{this.Guid}]");
                return null;
            }

            try
            {
                var result = await context.Env.InvokeNodeAsync(context, DataNode.Guid);
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
            data.KeyName = KeyName; // 变量名称

            nodeInfo.CustomData = data;
            return nodeInfo;
        }

        /// <summary>
        /// 加载全局变量的数据
        /// </summary>
        /// <param name="nodeInfo"></param>
        public override void LoadCustomData(NodeInfo nodeInfo)
        {
            KeyName = nodeInfo.CustomData?.KeyName;
        }

        /// <summary>
        /// 需要移除数据节点
        /// </summary>
        public override void Remove()
        {
            // 移除数据节点
            _ = this.Env.RemoveNodeAsync(DataNode?.Guid);
        }

    }
}
