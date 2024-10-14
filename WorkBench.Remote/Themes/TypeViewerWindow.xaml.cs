using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Serein.WorkBench.Themes
{
    /// <summary>
    /// TypeViewerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TypeViewerWindow : Window
    {
        public TypeViewerWindow()
        {
            InitializeComponent();
        }

        public Type Type { get; set; }

        public void LoadTypeInformation()
        {
            if (Type == null)
                return;

            NodeFlowDataObjectDetails typeNodeDetails = new NodeFlowDataObjectDetails
            {
                Name = Type.Name,
                DataType = Type,
            };
            var rootNode = new TreeViewItem { Header = Type.Name, Tag = typeNodeDetails };
            AddPlaceholderNode(rootNode); // 添加占位符节点
            TypeTreeView.Items.Clear();
            TypeTreeView.Items.Add(rootNode);

            rootNode.Expanded += TreeViewItem_Expanded; // 监听节点展开事件
        }

        /// <summary>
        /// 添加占位符节点
        /// </summary>
        private void AddPlaceholderNode(TreeViewItem node)
        {
            node.Items.Add(new TreeViewItem { Header = "Loading..." });
        }

        /// <summary>
        /// 节点展开事件，延迟加载子节点
        /// </summary>
        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            var item = (TreeViewItem)sender;

            // 如果已经加载过子节点，则不再重复加载
            if (item.Items.Count == 1 && item.Items[0] is TreeViewItem placeholder && placeholder.Header.ToString() == "Loading...")
            {
                item.Items.Clear();
                if (item.Tag is NodeFlowDataObjectDetails typeNodeDetails)
                {
                    AddMembersToTreeNode(item, typeNodeDetails.DataType);
                }
                
            }

            
        }

        /// <summary>
        /// 添加属性节点
        /// </summary>
        private void AddMembersToTreeNode(TreeViewItem node, Type type)
        {
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var member in members)
            {
                TreeViewItem memberNode = ConfigureTreeViewItem(member); // 生成类型节点的子项
                if (ConfigureTreeItemMenu(memberNode,member, out ContextMenu? contextMenu))
                {
                    memberNode.ContextMenu = contextMenu; // 设置子项节点的事件
                }

                node.Items.Add(memberNode); // 添加到父节点中
            }
        }


        /// <summary>
        /// 生成类型节点的子项
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        private TreeViewItem ConfigureTreeViewItem(MemberInfo member)
        {
            TreeViewItem memberNode = new TreeViewItem { Header = member.Name };
            if (member is PropertyInfo property)
            {
                NodeFlowDataObjectDetails typeNodeDetails = new NodeFlowDataObjectDetails
                {
                    ItemType = TreeItemType.Property,
                    DataType = property.PropertyType,
                    Name = property.Name,
                    DataValue = property,
                };
                memberNode.Tag = typeNodeDetails;

                var propertyType = typeNodeDetails.DataType;
                memberNode.Header = $"{member.Name} : {propertyType.Name}";
                
                if (!propertyType.IsPrimitive && propertyType != typeof(string))
                {
                    // 延迟加载类型的子属性，添加占位符节点
                    AddPlaceholderNode(memberNode);
                    memberNode.Expanded += TreeViewItem_Expanded; // 监听展开事件
                }
            }
            else if (member is MethodInfo method)
            {
                NodeFlowDataObjectDetails typeNodeDetails = new NodeFlowDataObjectDetails
                {
                    ItemType = TreeItemType.Method,
                    DataType = typeof(MethodInfo),
                    Name = method.Name,
                    DataValue = null,
                };
                memberNode.Tag = typeNodeDetails;

                var parameters = method.GetParameters();
                var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                memberNode.Header = $"{member.Name}({paramStr})";
            }
            else if (member is FieldInfo field)
            {
                NodeFlowDataObjectDetails typeNodeDetails = new NodeFlowDataObjectDetails
                {
                    ItemType = TreeItemType.Field,
                    DataType = field.FieldType,
                    Name = field.Name,
                    DataValue = field,
                };
                memberNode.Tag = typeNodeDetails;
                memberNode.Header = $"{member.Name} : {field.FieldType.Name}";
            }
            return memberNode;
        }


        /// <summary>
        /// 设置子项节点的事件
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        private bool ConfigureTreeItemMenu(TreeViewItem memberNode, MemberInfo member,out ContextMenu? contextMenu)
        {
            bool isChange = false;
            if (member is PropertyInfo property)
            {
                isChange = true;
                contextMenu = new ContextMenu();
                contextMenu.Items.Add(MainWindow.CreateMenuItem($"取值表达式", (s, e) =>
                {
                    string fullPath = GetNodeFullPath(memberNode);
                    string copyValue = "@Get " + fullPath;
                    Clipboard.SetDataObject(copyValue);
                }));
            }
            else if (member is MethodInfo method)
            {
                //isChange = true;
                contextMenu = new ContextMenu();
            }
            else if (member is FieldInfo field)
            {
                isChange = true; 
                contextMenu = new ContextMenu();
                contextMenu.Items.Add(MainWindow.CreateMenuItem($"取值表达式", (s, e) =>
                {
                    string fullPath = GetNodeFullPath(memberNode);
                    string copyValue = "@Get " + fullPath;
                    Clipboard.SetDataObject(copyValue);
                }));
            }
            else
            {
                contextMenu = new ContextMenu();
            }
            return isChange;
        }



        /// <summary>
        /// 获取当前节点的完整路径，例如 "node1.node2.node3.node4"
        /// </summary>
        /// <param name="node">目标节点</param>
        /// <returns>节点路径</returns>
        private string GetNodeFullPath(TreeViewItem node)
        {
            if (node == null)
                return string.Empty;

            NodeFlowDataObjectDetails typeNodeDetails = (NodeFlowDataObjectDetails)node.Tag;
            var parent = GetParentTreeViewItem(node);
            if (parent != null)
            {
                // 递归获取父节点的路径，并拼接当前节点的 Header
                return $"{GetNodeFullPath(parent)}.{typeNodeDetails.Name}";
            }
            else
            {
                // 没有父节点，则说明这是根节点，直接返回 Header
                return ""; 
                // return typeNodeDetails.Name.ToString();
            }
        }

        /// <summary>
        /// 获取指定节点的父级节点
        /// </summary>
        /// <param name="node">目标节点</param>
        /// <returns>父节点</returns>
        private TreeViewItem? GetParentTreeViewItem(TreeViewItem node)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(node);
            while (parent != null && parent is not TreeViewItem)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as TreeViewItem;
        }



        public class NodeFlowDataObjectDetails
        {
            /// <summary>
            /// 属性名称
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            /// 属性类型
            /// </summary>
            public TreeItemType ItemType { get; set; }
            /// <summary>
            /// 数据类型
            /// </summary>
            public Type DataType { get; set; }
            /// <summary>
            /// 数据（调试用？）
            /// </summary>
            public object DataValue { get; set; }
            /// <summary>
            /// 数据路径
            /// </summary>
            public string DataPath { get; set; }
        }

        public enum TreeItemType
        {
            Property,
            Method, 
            Field,
            IEnumerable,
            Item,
        }



    }

}
