using Serein.Library;
using Serein.Library.Api;
using System.Windows;
using System.Windows.Controls;

namespace Serein.Workbench.Themes
{
    /// <summary>
    /// NodeTreeVIewControl.xaml 的交互逻辑
    /// </summary>
    public partial class NodeTreeItemViewControl : UserControl
    {
        public NodeTreeItemViewControl()
        {
            InitializeComponent();
            foreach (var ct in NodeStaticConfig.ConnectionTypes)
            {
                var guid = ToGridView(this, ct);
                guid.Visibility = Visibility.Collapsed;
            }
        }


        /// <summary>
        ///  保存的节点数据
        /// </summary>
        private NodeModelBase nodeModel;
        private IFlowEnvironment flowEnvironment { get; set; }


        private class NodeTreeModel
        {
            public NodeModelBase RootNode { get; set; }
            public Dictionary<ConnectionType, List<NodeModelBase>> ChildNodes { get; set; }
        }


        public void InitAndLoadTree(IFlowEnvironment flowEnvironment, NodeModelBase nodeModel)
        {
            this.flowEnvironment = flowEnvironment;
            this.nodeModel = nodeModel;
            RefreshTree();
        }

        public TreeViewItem RefreshTree()
        {
            NodeModelBase rootNodeModel = this.nodeModel;
            NodeTreeModel nodeTreeModel = new NodeTreeModel
            {
                RootNode = rootNodeModel,
                ChildNodes = new Dictionary<ConnectionType, List<NodeModelBase>>()
                {
                    {ConnectionType.Upstream, []},
                    {ConnectionType.IsSucceed, [rootNodeModel]},
                    {ConnectionType.IsFail, []},
                    {ConnectionType.IsError, []},
                }
            };
            string? itemName = rootNodeModel.MethodDetails?.MethodTips;
            if (string.IsNullOrEmpty(itemName))
            {
                itemName = rootNodeModel.ControlType.ToString();
            }
            var rootNode = new TreeViewItem
            {
                Header = itemName,
                Tag = nodeTreeModel,
            };
            LoadNodeItem(this, nodeTreeModel);
            rootNode.Expanded += TreeViewItem_Expanded; // 监听展开事件
            rootNode.IsExpanded = true;
            return rootNode;
        }




        /// <summary>
        /// 展开子项事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem item && item.Tag is NodeTreeModel nodeTreeModel)
            {
                item.Items.Clear();
                NodeTreeItemViewControl? nodeTreeItemViewControl = LoadTNoderee(nodeTreeModel);

                if (nodeTreeItemViewControl is not null)
                {
                    LoadNodeItem(nodeTreeItemViewControl, nodeTreeModel);
                    item.Items.Add(nodeTreeItemViewControl);

                }
                item.IsSelected = false;
            }

            e.Handled = true;
        }

        /// <summary>
        /// 加载面板
        /// </summary>
        /// <param name="nodeTreeItemViewControl"></param>
        /// <param name="nodeTreeModel"></param>
        private void LoadNodeItem(NodeTreeItemViewControl nodeTreeItemViewControl, NodeTreeModel nodeTreeModel)
        {

            foreach (var ct in NodeStaticConfig.ConnectionTypes)
            {
                var treeViewer = ToTreeView(nodeTreeItemViewControl, ct);
                var guid = ToGridView(nodeTreeItemViewControl, ct);
                treeViewer.Items.Clear(); // 移除对象树的所有节点
                var list = nodeTreeModel.ChildNodes[ct];

                if (list.Count > 0)
                {
                    foreach (var child in list)
                    {
                        NodeTreeModel tmpNodeTreeModel = new NodeTreeModel
                        {
                            RootNode = child,
                            ChildNodes = child.SuccessorNodes,
                        };
                        string? itemName = child?.MethodDetails?.MethodTips;
                        if (string.IsNullOrEmpty(itemName))
                        {
                            itemName = child?.ControlType.ToString();
                        }
                        TreeViewItem treeViewItem = new TreeViewItem
                        {
                            Header = itemName,
                            Tag = tmpNodeTreeModel
                        };
                        treeViewItem.Expanded += TreeViewItem_Expanded;

                        var contextMenu = new ContextMenu();
                        contextMenu.Items.Add(MainWindow.CreateMenuItem("从此节点执行", (s, e) => 
                        {
                            flowEnvironment.StartAsyncInSelectNode(tmpNodeTreeModel.RootNode.Guid);
                        }));
                        contextMenu.Items.Add(MainWindow.CreateMenuItem("定位", (s, e) => flowEnvironment.NodeLocated(tmpNodeTreeModel.RootNode.Guid)));

                        treeViewItem.ContextMenu = contextMenu;
                        treeViewItem.Margin = new Thickness(-20, 0, 0, 0);
                        treeViewer.Items.Add(treeViewItem);
                    }
                    guid.Visibility = Visibility.Visible;
                }
                else
                {
                    guid.Visibility = Visibility.Collapsed;
                }
            }


        }

        /// <summary>
        /// 加载节点子项
        /// </summary>
        /// <param name="nodeTreeModel"></param>
        /// <returns></returns>
        private NodeTreeItemViewControl? LoadTNoderee(NodeTreeModel nodeTreeModel)
        {
            NodeTreeItemViewControl nodeTreeItemViewControl = null;
            foreach (var connectionType in NodeStaticConfig.ConnectionTypes)
            {
                var childNodeModels = nodeTreeModel.ChildNodes[connectionType];
                if (childNodeModels.Count > 0)
                {
                    nodeTreeItemViewControl ??= new NodeTreeItemViewControl();
                }
                else
                {
                    continue;
                }

                TreeView treeView = ToTreeView(nodeTreeItemViewControl, connectionType);
                foreach (var childNodeModel in childNodeModels)
                {
                    NodeTreeModel tempNodeTreeModel = new NodeTreeModel
                    {
                        RootNode = childNodeModel,
                        ChildNodes = childNodeModel.SuccessorNodes,
                    };

                    string? itemName = childNodeModel?.MethodDetails?.MethodTips;
                    if (string.IsNullOrEmpty(itemName))
                    {
                        itemName = childNodeModel?.ControlType.ToString();
                    }
                    TreeViewItem treeViewItem = new TreeViewItem
                    {
                        Header = itemName,
                        Tag = tempNodeTreeModel
                    };
                    treeViewItem.Margin = new Thickness(-20, 0, 0, 0);
                    treeViewItem.Visibility = Visibility.Visible;
                    treeView.Items.Add(treeViewItem);
                }
            }
            if (nodeTreeItemViewControl is not null)
            {
                foreach (var connectionType in NodeStaticConfig.ConnectionTypes)
                {
                    var childNodeModels = nodeTreeModel.ChildNodes[connectionType];
                    if (childNodeModels.Count > 0)
                    {
                        nodeTreeItemViewControl ??= new NodeTreeItemViewControl();
                    }
                    else
                    {
                        continue;
                    }
                }
            }
            return nodeTreeItemViewControl;
        }

        /// <summary>
        /// 折叠事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeViewItem_Collapsed(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem item && item.Items.Count > 0)
            {
                item.Items.Clear();
            }
        }
        public static TreeView ToTreeView(NodeTreeItemViewControl item, ConnectionType connectionType)
        {
            return connectionType switch
            {
                ConnectionType.Upstream => item.UpstreamTreeNodes,
                ConnectionType.IsError => item.IsErrorTreeNodes,
                ConnectionType.IsFail => item.IsFailTreeNodes,
                ConnectionType.IsSucceed => item.IsSucceedTreeNodes,
                _ => throw new Exception("LoadNodeItem Error ：ConnectionType is " + connectionType)
            };
        }
        public static Grid ToGridView(NodeTreeItemViewControl item, ConnectionType connectionType)
        {
            return connectionType switch
            {
                ConnectionType.Upstream => item.UpstreamTreeGuid,
                ConnectionType.IsError => item.IsErrorTreeGuid,
                ConnectionType.IsFail => item.IsFailTreeGuid,
                ConnectionType.IsSucceed => item.IsSucceedTreeGuid,
                _ => throw new Exception("LoadNodeItem Error ：ConnectionType is " + connectionType)
            };
        }

        //public static System.Windows.Shapes.Rectangle ToRectangle(NodeTreeItemViewControl item, ConnectionType connectionType)
        //{
        //    return connectionType switch
        //    {
        //        ConnectionType.Upstream => item.UpstreamTreeRectangle,
        //        ConnectionType.IsError => item.IsErrorRectangle,
        //        ConnectionType.IsFail => item.IsFailRectangle,
        //        ConnectionType.IsSucceed => item.IsSucceedRectangle,
        //        _ => throw new Exception("LoadNodeItem Error ：ConnectionType is " + connectionType)
        //    };
        //}


    }
}
