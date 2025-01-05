using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.Library.Utils.SereinExpression;
using System;
using System.Collections;
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
using System.Windows.Markup.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using static Serein.Workbench.Themes.TypeViewerWindow;

namespace Serein.Workbench.Themes
{

    public class FlowDataDetails
    {
        /// <summary>
        /// 属性名称
        /// </summary>
        public string? Name { get; set; }
        /// <summary>
        /// 属性类型
        /// </summary>
        public TreeItemType ItemType { get; set; }
        /// <summary>
        /// 数据类型
        /// </summary>
        public Type? DataType { get; set; }
        /// <summary>
        /// 数据
        /// </summary>
        public object? DataValue { get; set; }
        /// <summary>
        /// 数据路径
        /// </summary>
        public string DataPath { get; set; } = string.Empty;
    }


    /// <summary>
    /// ObjectViewerControl.xaml 的交互逻辑
    /// </summary>
    public partial class ObjectViewerControl : UserControl
    {
        public ObjectViewerControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 监视类型
        /// </summary>
        public enum MonitorType
        {
            /// <summary>
            /// 作用于对象（对象的引用）的监视
            /// </summary>
            NodeFlowData,
            /// <summary>
            /// 作用与节点（FLowData）的监视
            /// </summary>
            IOCObj,
        }

        /// <summary>
        /// 运行环境
        /// </summary>
        public IFlowEnvironment? FlowEnvironment { get; set; }

        /// <summary>
        /// 监视对象的键
        /// </summary>
        public string? MonitorKey { get => monitorKey; }
        /// <summary>
        /// 正在监视的对象
        /// </summary>
        public object? MonitorObj { get => monitorObj; }

        /// <summary>
        /// 监视表达式
        /// </summary>
        public string? MonitorExpression { get => ExpressionTextBox.Text.ToString(); }

        private string? monitorKey;
        private object? monitorObj;

        // 用于存储当前展开的节点路径
        private HashSet<string> expandedNodePaths = new HashSet<string>();
        

        /// <summary>
        /// 加载对象信息，展示其成员
        /// </summary>
        /// <param name="obj">要展示的对象</param>
        public void LoadObjectInformation(string key, object obj)
        {
            if (obj == null) return;
            monitorKey = key;
            monitorObj = obj;
            expandedNodePaths.Clear();
            LoadTree(obj);
        }

        /// <summary>
        /// 刷新对象
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshObjectTree(monitorObj);
        }

        /// <summary>
        /// 更新表达式
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UpMonitorExpressionButton_Click(object sender, RoutedEventArgs e)
        {
            //if (FlowEnvironment is not null && await FlowEnvironment.AddInterruptExpressionAsync(monitorKey, MonitorExpression)) // 对象预览器尝试添加中断表达式
            //{
            //    if (string.IsNullOrEmpty(MonitorExpression))
            //    {
            //        ExpressionTextBox.Text = "表达式已清空";
            //    }
            //    else
            //    {
            //        UpMonitorExpressionButton.Content = "更新监视表达式";
            //    }
            //}
        }

        private TreeViewItem? LoadTree(object? obj)
        {
            if (obj is null) return null;
            var objectType = obj.GetType();
            FlowDataDetails flowDataDetails = new FlowDataDetails
            {
                Name = objectType.Name,
                DataType = objectType,
                DataValue = obj,
                DataPath = ""
            };
            var rootNode = new TreeViewItem
            {
                Header = objectType.Name,
                Tag = flowDataDetails,
            };
            
            
            ObjectTreeView.Items.Clear(); // 移除对象树的所有节点
            ObjectTreeView.Items.Add(rootNode); // 添加所有节点
            rootNode.Expanded += TreeViewItem_Expanded; // 监听展开事件
            rootNode.Collapsed += TreeViewItem_Collapsed; // 监听折叠事件
            // 这里创建了一个子项，并给这个子项创建了“正在加载”的子项
            // 然后移除了原来对象树的所有项，再把这个新创建的子项添加上去
            // 绑定了展开/折叠事件后，自动展开第一层，开始反射obj的成员，并判断obj的成员生成什么样的节点
            rootNode.IsExpanded = true;
            return rootNode;
        }

        /// <summary>
        /// 刷新对象属性树
        /// </summary>
        public void RefreshObjectTree(object? obj)
        {
            monitorObj = obj;
            var rootNode =  LoadTree(obj);
            if (rootNode is not null)
            {
                ExpandPreviouslyExpandedNodes(rootNode); // 遍历节点，展开之前记录的节点

            }
           
        }

        /// <summary>
        /// 展开父节点，如果路径存在哈希记录，则将其自动展开，并递归展开后的子节点。
        /// </summary>
        /// <param name="node"></param>
        private void ExpandPreviouslyExpandedNodes(TreeViewItem node)
        {
            if (node == null) return;
            if(node.Tag is FlowDataDetails flowDataDetails)
            {
                if (expandedNodePaths.Contains(flowDataDetails.DataPath))
                {
                    node.IsExpanded = true;
                }
            }
            
            foreach (TreeViewItem child in node.Items)
            {
                ExpandPreviouslyExpandedNodes(child);
            }
        }

        /// <summary>
        /// 展开子项事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private  void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                if (item.Tag is FlowDataDetails flowDataDetails) // FlowDataDetails flowDataDetails  object obj
                {
                    if (flowDataDetails.ItemType != TreeItemType.Item && item.Items.Count != 0)
                    {
                        return;
                    }
                    if(flowDataDetails.DataValue is null || flowDataDetails.DataType is null)
                    {
                        return;
                    }

                    // 记录当前节点的路径
                    var path = flowDataDetails.DataPath;
                    expandedNodePaths.Add(path);
                    AddMembersToTreeNode(item, flowDataDetails.DataValue, flowDataDetails.DataType);

                }
            }
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
                if (item.Tag is FlowDataDetails flowDataDetails) 
                {
                    // 记录当前节点的路径
                    var path = flowDataDetails.DataPath;
                    if(path != "")
                    {
                        expandedNodePaths.Remove(path);
                    }
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
            // 获取公开的属性
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var member in members)
            {
                if (member.Name.StartsWith(".") ||
                    member.Name.StartsWith("get_") ||
                    member.Name.StartsWith("set_")
                    )
                {
                    // 跳过构造函数、属性的get/set方法
                    continue;
                }

                TreeViewItem? memberNode = ConfigureTreeViewItem(obj, member); // 根据对象成员生成节点对象
                if (memberNode is not null)
                {
                    treeViewNode.Items.Add(memberNode); // 添加到当前节点

                    // 配置数据路径
                    FlowDataDetails subFlowDataDetails = (FlowDataDetails)memberNode.Tag;
                    string superPath = ((FlowDataDetails)treeViewNode.Tag).DataPath;
                    string subPath = superPath + "." + subFlowDataDetails.Name;
                    subFlowDataDetails.DataPath = subPath;

                    // 配置右键菜单
                    var contextMenu = new ContextMenu();
                    contextMenu.Items.Add(MainWindow.CreateMenuItem($"表达式", (s, e) =>
                    {
                        ExpressionTextBox.Text = subPath; // 获取表达式

                    }));
                    memberNode.ContextMenu = contextMenu;
                }
            }
        }

        /// <summary>
        /// 配置节点子项
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="member"></param>
        /// <returns></returns>
        private TreeViewItem? ConfigureTreeViewItem(object obj, MemberInfo member)
        {
            if (obj == null)
            {
                return null;
            }
            #region 属性
            if (member is PropertyInfo property)
            {
                #region 集合类型(非字符串）
                if (property.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.GetValue(obj) is IEnumerable collection && collection is not null)
                {
                    TreeViewItem memberNode = new TreeViewItem { Header = member.Name };
                    // 处理集合类型的属性
                    memberNode.Tag = new FlowDataDetails
                    {
                        ItemType = TreeItemType.IEnumerable,
                        DataType = property.PropertyType,
                        Name = property.Name,
                        DataValue = collection,
                    };

                    int index = 0;
                    foreach (var item in collection)
                    {
                        var itemNode = new TreeViewItem { Header = $"[{index++}] {item}" ?? "null" };
                        memberNode.Tag = new FlowDataDetails
                        {
                            ItemType = TreeItemType.Item,
                            DataType = item?.GetType(),
                            Name = property.Name,
                            DataValue = itemNode,
                        };
                        memberNode.Items.Add(itemNode);
                    }
                    memberNode.Header = $"{property.Name} : {property.PropertyType.Name} [{index}]";
                    if (!property.PropertyType.IsPrimitive && property.PropertyType != typeof(string))
                    {
                        memberNode.Expanded += TreeViewItem_Expanded;
                        memberNode.Collapsed += TreeViewItem_Collapsed;
                    }
                    return memberNode;
                }
                #endregion
                #region 值类型与未判断的类型
                else
                {
                    TreeViewItem memberNode = new TreeViewItem { Header = member.Name };
                    string propertyValue = GetPropertyValue(obj, property, out object? value);
                    memberNode.Tag = new FlowDataDetails
                    {
                        ItemType = TreeItemType.Property,
                        DataType = property.PropertyType,
                        Name = property.Name,
                        DataValue = value,
                    }; ;

                    memberNode.Header = $"{property.Name} : {property.PropertyType.Name} = {propertyValue}";
                    if (!property.PropertyType.IsPrimitive && property.PropertyType != typeof(string))
                    {
                        memberNode.Expanded += TreeViewItem_Expanded;
                        memberNode.Collapsed += TreeViewItem_Collapsed;
                    }
                    return memberNode;
                } 

                #endregion
            }
            #endregion
            #region 字段
            else if (member is FieldInfo field)
            {
                #region 集合类型(非字符串）
                if (field.FieldType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(field.FieldType) && field.GetValue(obj) is IEnumerable collection && collection is not null)
                {
                    TreeViewItem memberNode = new TreeViewItem { Header = member.Name };
                    // 处理集合类型的字段
                    memberNode.Tag = new FlowDataDetails
                    {
                        ItemType = TreeItemType.IEnumerable,
                        DataType = field.FieldType,
                        Name = field.Name,
                        DataValue = collection,
                    };

                    int index = 0;
                    foreach (var item in collection)
                    {
                        var itemNode = new TreeViewItem { Header = $"[{index++}] {item}" ?? "null" };
                        memberNode.Tag = new FlowDataDetails
                        {
                            ItemType = TreeItemType.Item,
                            DataType = item?.GetType(),
                            Name = field.Name,
                            DataValue = itemNode,
                        };
                        //collectionNode.Items.Add(itemNode);
                        memberNode.Items.Add(itemNode);
                    }
                    memberNode.Header = $"{field.Name} : {field.FieldType.Name} [{index}]";
                    if (!field.FieldType.IsPrimitive && field.FieldType != typeof(string))
                    {
                        memberNode.Expanded += TreeViewItem_Expanded;
                        memberNode.Collapsed += TreeViewItem_Collapsed;
                    }
                    return memberNode;
                }
                #endregion
                #region 值类型与未判断的类型
                else
                {
                    TreeViewItem memberNode = new TreeViewItem { Header = member.Name };
                    string fieldValue = GetFieldValue(obj, field, out object? value);

                    memberNode.Tag = new FlowDataDetails
                    {
                        ItemType = TreeItemType.Field,
                        DataType = field.FieldType,
                        Name = field.Name,
                        DataValue = value,

                    };
                    memberNode.Header = $"{field.Name} : {field.FieldType.Name} = {fieldValue}";

                    if (!field.FieldType.IsPrimitive && field.FieldType != typeof(string))
                    {
                        memberNode.Expanded += TreeViewItem_Expanded;
                        memberNode.Collapsed += TreeViewItem_Collapsed;
                    }
                    return memberNode;
                } 
                #endregion
            }
            #endregion
            #region 返回null
            else
            {
                return null;
            } 
            #endregion

        }

        /// <summary>
        /// 获取属性类型的成员
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        private string GetPropertyValue(object obj, PropertyInfo property,out object? value)
        { 
            try
            {
                if(obj is null)
                {
                    value = null;
                    return "Error";
                }
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
        private string GetFieldValue(object obj, FieldInfo field, out object? value)
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

    }
}


/// <summary>
/// 上次刷新时间
/// </summary>
//private DateTime lastRefreshTime = DateTime.MinValue; 
/// <summary>
/// 刷新间隔
/// </summary>
//private readonly TimeSpan refreshInterval = TimeSpan.FromSeconds(0.1);  
// 当前时间
//var currentTime = DateTime.Now;
//if (currentTime - lastRefreshTime < refreshInterval)
//{
//    return; // 跳过过于频繁的刷新调用
//}
//else
//{
//    lastRefreshTime = currentTime;// 记录这次的刷新时间
//}
//

/// <summary>
/// 从当前节点获取至父节点的路径，例如 "node1.node2.node3.node4"
/// </summary>
/// <param name="node">目标节点</param>
/// <returns>节点路径</returns>
//private string GetNodeFullPath(TreeViewItem node)
//{
//    if (node == null)
//        return string.Empty;

//    FlowDataDetails flowDataDetails = (FlowDataDetails)node.Tag;
//    var parent = GetParentTreeViewItem(node);
//    if (parent != null)
//    {
//        // 递归获取父节点的路径，并拼接当前节点的 Header
//        return $"{GetNodeFullPath(parent)}.{flowDataDetails.Name}";
//    }
//    else
//    {
//        // 没有父节点，则说明这是根节点，直接返回 Header
//        return "";
//    }
//}

/// <summary>
/// 获取指定节点的父级节点
/// </summary>
/// <param name="node">目标节点</param>
/// <returns>父节点</returns>
//private  TreeViewItem GetParentTreeViewItem(TreeViewItem node)
//{
//    DependencyObject parent = VisualTreeHelper.GetParent(node);
//    while (parent != null && !(parent is TreeViewItem))
//    {
//        parent = VisualTreeHelper.GetParent(parent);
//    }
//    return parent as TreeViewItem;
//}






/// <summary>
/// 根据成员类别配置右键菜单
/// </summary>
/// <param name="memberNode"></param>
/// <param name="member"></param>
/// <param name="contextMenu"></param>
/// <returns></returns>
//private  bool ConfigureTreeItemMenu(TreeViewItem memberNode, MemberInfo member, out ContextMenu? contextMenu)
//{
//    if (ConfigureTreeItemMenu(memberNode, member, out ContextMenu? contextMenu))
//    {
//        memberNode.ContextMenu = contextMenu; // 设置子项节点的事件
//    }

//    bool isChange = false;
//    if (member is PropertyInfo property)
//    {
//        isChange = true;
//        contextMenu = new ContextMenu();
//        contextMenu.Items.Add(MainWindow.CreateMenuItem($"表达式", (s, e) =>
//        {
//            string fullPath = GetNodeFullPath(memberNode);
//            string copyValue = /*"@Get " + */fullPath;
//            ExpressionTextBox.Text = copyValue;
//            // Clipboard.SetDataObject(copyValue);

//        }));
//    }
//    else if (member is MethodInfo method)
//    {
//        //isChange = true;
//        contextMenu = new ContextMenu();
//    }
//    else if (member is FieldInfo field)
//    {
//        isChange = true;
//        contextMenu = new ContextMenu();
//        contextMenu.Items.Add(MainWindow.CreateMenuItem($"表达式", (s, e) =>
//        {
//            string fullPath = GetNodeFullPath(memberNode);
//            string copyValue = /*"@Get " +*/ fullPath;
//            ExpressionTextBox.Text = copyValue;
//            // Clipboard.SetDataObject(copyValue);
//        }));
//    }
//    else
//    {
//        contextMenu = new ContextMenu();
//    }
//    return isChange;
//}





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

