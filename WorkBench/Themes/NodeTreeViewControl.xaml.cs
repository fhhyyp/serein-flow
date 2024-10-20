using Serein.Library;
using Serein.Library.Api;
using System.Windows.Controls;

namespace Serein.Workbench.Themes
{
    /// <summary>
    /// NodeTreeViewControl.xaml 的交互逻辑
    /// </summary>
    public partial class NodeTreeViewControl : UserControl
    {
        public NodeTreeViewControl()
        {
            InitializeComponent();
        }

        private string startNodeGuid = string.Empty;
        private Dictionary<string, NodeTreeItemViewControl> globalFlipflopNodes = [];
        private Dictionary<string, NodeTreeItemViewControl> unemployedNodes = [];

        public void LoadNodeTreeOfStartNode(IFlowEnvironment flowEnvironment, NodeModelBase nodeModel)
        {
            startNodeGuid = nodeModel.Guid;
            StartNodeViewer.InitAndLoadTree(flowEnvironment, nodeModel);
        }

        #region 触发器
        public void AddGlobalFlipFlop(IFlowEnvironment flowEnvironment, NodeModelBase nodeModel)
        {
            if (!globalFlipflopNodes.ContainsKey(nodeModel.Guid))
            {
                NodeTreeItemViewControl flipflopTreeViewer = new NodeTreeItemViewControl();
                flipflopTreeViewer.InitAndLoadTree(flowEnvironment, nodeModel);
                globalFlipflopNodes.Add(nodeModel.Guid, flipflopTreeViewer);
                GlobalFlipflopNodeListbox.Items.Add(flipflopTreeViewer);
            }
        }
        public void RefreshGlobalFlipFlop(NodeModelBase nodeModel)
        {
            if (globalFlipflopNodes.TryGetValue(nodeModel.Guid, out var viewer))
            {
                viewer.RefreshTree();
            }
        }
        public void RemoteGlobalFlipFlop(NodeModelBase nodeModel)
        {
            if (globalFlipflopNodes.TryGetValue(nodeModel.Guid, out var viewer))
            {
                globalFlipflopNodes.Remove(nodeModel.Guid);
                GlobalFlipflopNodeListbox.Items.Remove(viewer);
            }
        }
        #endregion


        #region 无业游民（定义：不存在于起始节点与全局触发器的调用链上的节点，只能手动刷新？）
        public void AddUnemployed(IFlowEnvironment flowEnvironment, NodeModelBase nodeModel)
        {
            if (!unemployedNodes.ContainsKey(nodeModel.Guid))
            {
                NodeTreeItemViewControl flipflopTreeViewer = new NodeTreeItemViewControl();
                flipflopTreeViewer.InitAndLoadTree(flowEnvironment, nodeModel);
                unemployedNodes.Add(nodeModel.Guid, flipflopTreeViewer);
                GlobalFlipflopNodeListbox.Items.Add(flipflopTreeViewer);
            }
        }
        public void RefreshUnemployed(NodeModelBase nodeModel)
        {
            if (unemployedNodes.TryGetValue(nodeModel.Guid, out var viewer))
            {
                viewer.RefreshTree();
            }
        }
        public void RemoteUnemployed(NodeModelBase nodeModel)
        {
            if (unemployedNodes.TryGetValue(nodeModel.Guid, out var viewer))
            {
                unemployedNodes.Remove(nodeModel.Guid);
                GlobalFlipflopNodeListbox.Items.Remove(viewer);
            }
        }
        #endregion

    }
}
