using Serein.Library;
using Serein.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow
{
    /// <summary>
    /// 节点类型
    /// </summary>
    public class NodeMVVM
    {
        /// <summary>
        /// 节点类型
        /// </summary>
        public required NodeControlType NodeType { get; set; }

        /// <summary>
        /// 节点Model类型
        /// </summary>
        public required Type ModelType {  get; set; }

        /// <summary>
        /// 节点视图控件类型
        /// </summary>
        public Type? ControlType {  get; set; }

        /// <summary>
        /// 节点视图VM类型
        /// </summary>
        public Type? ViewModelType {  get; set; }

        public override string ToString()
        {
            return $"$[{NodeType}]类型信息 : ModelType->{ModelType};ControlType->{ControlType};ViewModelType->{ViewModelType}";
        }
    }

    /// <summary>
    /// 节点 数据、视图、VM 管理
    /// </summary>
    public static class NodeMVVMManagement
    {
        /// <summary>
        /// 节点对应的控件类型
        /// </summary>
        private static ConcurrentDictionary<NodeControlType, NodeMVVM> FlowNodeTypes { get; } = [];

        /// <summary>
        /// 注册 Model 类型
        /// </summary>
        /// <param name="type"></param>
        /// <param name="modelType"></param>
        public static bool RegisterModel(NodeControlType type, Type modelType)
        {
            if(FlowNodeTypes.TryGetValue(type,out var nodeMVVM))
            {
                SereinEnv.WriteLine(InfoType.WARN, $"无法为节点[{type}]注册Model类型[{modelType}]，已经注册的类型为{nodeMVVM}。");
                return false;
            }
            nodeMVVM = new NodeMVVM 
            { 
                NodeType = type,
                ModelType = modelType
            };
            return FlowNodeTypes.TryAdd(type, nodeMVVM);
        }

        /// <summary>
        /// 注册 UI 类型
        /// </summary>
        /// <param name="type"></param>
        /// <param name="controlType"></param>
        /// <param name="viewModelType"></param>
        public static bool RegisterUI(NodeControlType type, Type controlType,Type viewModelType)
        {
            if (!FlowNodeTypes.TryGetValue(type, out var nodeMVVM))
            {
                SereinEnv.WriteLine(InfoType.WARN, $"无法为节点[{type}]注册UI类型[{controlType}][{viewModelType}]，当前类型尚未注册。");
                return false;
            }
            nodeMVVM.ControlType = controlType;
            nodeMVVM.ViewModelType = viewModelType;
            return true;
        }

        /// <summary>
        /// 获取相应的类型
        /// </summary>
        /// <param name="type"></param>
        /// <param name="nodeMVVM"></param>
        /// <returns></returns>
        public static bool TryGetType(NodeControlType type, out NodeMVVM nodeMVVM)
        {
            if( FlowNodeTypes.TryGetValue(type, out nodeMVVM))
            {
                return nodeMVVM != null;
            }
            else
            {

                return false;
            }
        }
    }
}
