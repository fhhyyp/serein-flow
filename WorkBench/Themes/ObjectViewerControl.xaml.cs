using Serein.Library.Api;
using Serein.NodeFlow.Base;
using Serein.NodeFlow.Tool.SereinExpression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using static Serein.WorkBench.Themes.TypeViewerWindow;

namespace Serein.WorkBench.Themes
{

    public class FlowDataDetails
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
        /// 数据
        /// </summary>
        public object DataValue { get; set; }
        /// <summary>
        /// 数据路径
        /// </summary>
        public string DataPath { get; set; }
    }


    /// <summary>
    /// ObjectViewerControl.xaml 的交互逻辑
    /// </summary>
    public partial class ObjectViewerControl : UserControl
    {
        private object _objectInstance;
        public string NodeGuid { get;set; }
        public string MonitorExpression { get => ExpressionTextBox.Text.ToString(); }
        public IFlowEnvironment FlowEnvironment { get;set; }
        public NodeModelBase NodeModel { get;set; }

        public ObjectViewerControl()
        {
            InitializeComponent();
        }

        private DateTime _lastRefreshTime = DateTime.MinValue;  // 上次刷新时间
        private TimeSpan _refreshInterval = TimeSpan.FromSeconds(0.1);  // 刷新间隔（2秒）

        /// <summary>
        /// 加载对象信息，展示其成员
        /// </summary>
        /// <param name="obj">要展示的对象</param>
        public void LoadObjectInformation(object obj)
        {
            if (obj == null)
                return;


            // 当前时间
            var currentTime = DateTime.Now;

            // 如果上次刷新时间和当前时间之间的差值小于设定的间隔，则跳过
            if (currentTime - _lastRefreshTime < _refreshInterval)
            {
                // 跳过过于频繁的刷新调用
                return;
            }

            // 记录这次的刷新时间
            _lastRefreshTime = currentTime;

            _objectInstance = obj;
            RefreshObjectTree(obj);
        }

        ///// <summary>
        ///// 添加表达式
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <param name="e"></param>
        //private void AddMonitorExpressionButton_Click(object sender, RoutedEventArgs e)
        //{

        //    OpenInputDialog((exp) =>
        //    {
        //        FlowEnvironment.AddInterruptExpression(NodeGuid, exp);
        //    });
        //}


        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            //RefreshObjectTree(_objectInstance);
            FlowEnvironment.SetNodeFLowDataMonitorState(NodeGuid, true);
        }

        private void UpMonitorExpressionButton_Click(object sender, RoutedEventArgs e)
        {
            //MonitorExpression = ExpressionTextBox.Text.ToString();

            if(FlowEnvironment.AddInterruptExpression(NodeGuid, MonitorExpression))
            {
                if (string.IsNullOrEmpty(MonitorExpression))
                {
                    ExpressionTextBox.Text = "表达式已清空";
                }
                else
                {
                    UpMonitorExpressionButton.Content = "更新监视表达式";
                }
            }

        }



        // 用于存储当前展开的节点路径
        private  HashSet<string> _expandedNodePaths = new HashSet<string>();

        /// <summary>
        /// 刷新对象属性树
        /// </summary>
        public void RefreshObjectTree(object obj)
        {
            if (obj is null)
                return;
            // 当前时间
            var currentTime = DateTime.Now;

            // 如果上次刷新时间和当前时间之间的差值小于设定的间隔，则跳过
            if (currentTime - _lastRefreshTime < _refreshInterval)
            {
                // 跳过过于频繁的刷新调用
                return;
            }

            // 记录这次的刷新时间
            _lastRefreshTime = currentTime;

            var objectType = obj.GetType();

            FlowDataDetails flowDataDetails = new FlowDataDetails
            {
                Name = objectType.Name,
                DataType = objectType,
                DataValue = obj
            };
            var rootNode = new TreeViewItem 
            { 
                Header = objectType.Name, 
                Tag = flowDataDetails,
            };

            // 添加占位符节点
            AddPlaceholderNode(rootNode);
            ObjectTreeView.Items.Clear();
            ObjectTreeView.Items.Add(rootNode);

            // 监听展开事件
            rootNode.Expanded += TreeViewItem_Expanded;

            // 自动展开第一层
            rootNode.IsExpanded = true; // 直接展开根节点

            // 加载根节点的属性和字段
            if (rootNode.Items.Count == 1 && rootNode.Items[0] is TreeViewItem placeholder && placeholder.Header.ToString() == "Loading...")
            {
                rootNode.Items.Clear();
                AddMembersToTreeNode(rootNode, obj, objectType);
            }
            // 遍历节点，展开之前记录的节点
            ExpandPreviouslyExpandedNodes(rootNode);
        }

        // 遍历并展开之前记录的节点
        private  void ExpandPreviouslyExpandedNodes(TreeViewItem node)
        {
            if (_expandedNodePaths.Contains(GetNodeFullPath(node)))
            {
                node.IsExpanded = true;
            }

            foreach (TreeViewItem child in node.Items)
            {
                ExpandPreviouslyExpandedNodes(child);
            }
        }







        /// <summary>
        /// 添加父节点
        /// </summary>
        /// <param name="node"></param>
        private  void AddPlaceholderNode(TreeViewItem node)
        {
            node.Items.Add(new TreeViewItem { Header = "Loading..." });
        }

        /// <summary>
        /// 展开子项事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private  void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            var item = (TreeViewItem)sender;

            if (item.Items.Count == 1 && item.Items[0] is TreeViewItem placeholder && placeholder.Header.ToString() == "Loading...")
            {
                item.Items.Clear();
                if (item.Tag is FlowDataDetails flowDataDetails) // FlowDataDetails flowDataDetails  object obj
                {
                    // 记录当前节点的路径
                    _expandedNodePaths.Add(GetNodeFullPath(item));
                    AddMembersToTreeNode(item, flowDataDetails.DataValue, flowDataDetails.DataType);
                }
            }
        }

        /// <summary>
        /// 反射对象数据添加子节点
        /// </summary>
        /// <param name="treeViewNode"></param>
        /// <param name="obj"></param>
        /// <param name="type"></param>
        private  void AddMembersToTreeNode(TreeViewItem treeViewNode, object obj, Type type)
        {
            // 获取属性和字段
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var member in members)
            {
                TreeViewItem memberNode = ConfigureTreeViewItem(obj, member);
                treeViewNode.Items.Add(memberNode);
                if (ConfigureTreeItemMenu(memberNode, member, out ContextMenu? contextMenu))
                {
                    memberNode.ContextMenu = contextMenu; // 设置子项节点的事件

                }
                


            }
        }

        /// <summary>
        /// 配置右键菜单功能
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="member"></param>
        /// <returns></returns>
        private  TreeViewItem ConfigureTreeViewItem(object obj, MemberInfo member)
        {
            TreeViewItem memberNode = new TreeViewItem { Header = member.Name };

            if (member is PropertyInfo property)
            {

                string propertyValue = GetPropertyValue(obj, property,out object value);
                FlowDataDetails flowDataDetails = new FlowDataDetails
                {
                    ItemType = TreeItemType.Property,
                    DataType = property.PropertyType,
                    Name = property.Name,
                    DataValue = value,
                    DataPath = GetNodeFullPath(memberNode),
                };

                memberNode.Tag = flowDataDetails;

                memberNode.Header = $"{property.Name} : {property.PropertyType.Name} = {propertyValue}";

                if (!property.PropertyType.IsPrimitive && property.PropertyType != typeof(string))
                {
                    AddPlaceholderNode(memberNode);
                    memberNode.Expanded += TreeViewItem_Expanded;
                }
            }
            else if (member is FieldInfo field)
            {

                string fieldValue = GetFieldValue(obj, field, out object value);
                FlowDataDetails flowDataDetails = new FlowDataDetails
                {
                    ItemType = TreeItemType.Field,
                    DataType = field.FieldType,
                    Name = field.Name,
                    DataValue = value,
                    DataPath = GetNodeFullPath(memberNode),
                };

                memberNode.Tag = flowDataDetails;

                memberNode.Header = $"{field.Name} : {field.FieldType.Name} = {fieldValue}";

                if (!field.FieldType.IsPrimitive && field.FieldType != typeof(string))
                {
                    AddPlaceholderNode(memberNode);
                    memberNode.Expanded += TreeViewItem_Expanded;
                }
            }

            return memberNode;
        }

        /// <summary>
        /// 获取属性类型的成员
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        private  string GetPropertyValue(object obj, PropertyInfo property,out object value)
        { 
            try
            {

                var properties = obj.GetType().GetProperties();



                // 获取实例属性值
                value = property.GetValue(obj);
                return value?.ToString() ?? "null"; // 返回值或“null”
            }
            catch
            {
                value = null;
                return "Error";
            }
        }


        /// <summary>
        /// 获取字段类型的成员
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="field"></param>
        /// <returns></returns>
        private  string GetFieldValue(object obj, FieldInfo field, out object value)
        {
            try
            {
                value = field.GetValue(obj);
                return value?.ToString() ?? "null";
            }
            catch
            {
                value = null;
                return "Error";
            }
        }

        /// <summary>
        /// 根据成员类别配置右键菜单
        /// </summary>
        /// <param name="memberNode"></param>
        /// <param name="member"></param>
        /// <param name="contextMenu"></param>
        /// <returns></returns>
        private  bool ConfigureTreeItemMenu(TreeViewItem memberNode, MemberInfo member, out ContextMenu? contextMenu)
        {
            bool isChange = false;
            if (member is PropertyInfo property)
            {
                isChange = true;
                contextMenu = new ContextMenu();
                contextMenu.Items.Add(MainWindow.CreateMenuItem($"表达式", (s, e) =>
                {
                    string fullPath = GetNodeFullPath(memberNode);
                    string copyValue = /*"@Get " + */fullPath;
                    ExpressionTextBox.Text = copyValue;
                    // Clipboard.SetDataObject(copyValue);

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
                contextMenu.Items.Add(MainWindow.CreateMenuItem($"表达式", (s, e) =>
                {
                    string fullPath = GetNodeFullPath(memberNode);
                    string copyValue = /*"@Get " +*/ fullPath;
                    ExpressionTextBox.Text = copyValue;
                    // Clipboard.SetDataObject(copyValue);
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
        private  string GetNodeFullPath(TreeViewItem node)
        {
            if (node == null)
                return string.Empty;

            FlowDataDetails flowDataDetails = (FlowDataDetails)node.Tag;
            var parent = GetParentTreeViewItem(node);
            if (parent != null)
            {
                // 递归获取父节点的路径，并拼接当前节点的 Header
                return $"{GetNodeFullPath(parent)}.{flowDataDetails.Name}";
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
        private  TreeViewItem GetParentTreeViewItem(TreeViewItem node)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(node);
            while (parent != null && !(parent is TreeViewItem))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as TreeViewItem;
        }



        private  InputDialog OpenInputDialog(Action<string> action)
        {
            var inputDialog = new InputDialog();
            inputDialog.Closed += (s, e) =>
            {
                if (inputDialog.DialogResult == true)
                {
                    string userInput = inputDialog.InputValue;
                    action?.Invoke(userInput);
                }
            };
            inputDialog.ShowDialog();
            return inputDialog;
            
        }





        ///// <summary>
        ///// 刷新按钮的点击事件
        ///// </summary>
        //private void RefreshButton_Click(object sender, RoutedEventArgs e)
        //{
        //    RefreshObjectTree();
        //}

        //private bool IsTimerRefres = false;
        //private void TimerRefreshButton_Click(object sender, RoutedEventArgs e)
        //{
        //    if (IsTimerRefres)
        //    {
        //        IsTimerRefres = false;
        //        TimerRefreshButton.Content = "定时刷新";
        //    }
        //    else
        //    {
        //        IsTimerRefres = true;
        //        TimerRefreshButton.Content = "取消刷新";

        //       _ = Task.Run(async () => {
        //           while (true)
        //           {
        //               if (IsTimerRefres)
        //               {
        //                   Application.Current.Dispatcher.Invoke(() =>
        //                   {
        //                       RefreshObjectTree(); // 刷新UI
        //                   }); 
        //                   await Task.Delay(100);
        //               }
        //               else
        //               {
        //                   break;
        //               }
        //           }
        //           IsTimerRefres = false;
        //       });
        //    }

        //}





    }
}


