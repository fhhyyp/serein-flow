using Serein.Library.Api;
using Serein.NodeFlow.Base;
using Serein.NodeFlow.Tool.SereinExpression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using static Serein.WorkBench.Themes.TypeViewerWindow;
using static System.Collections.Specialized.BitVector32;

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
        public IFlowEnvironment FlowEnvironment { get;set; }

        // private NodeModelBase _nodeFlowData;

        public ObjectViewerControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 加载对象信息，展示其成员
        /// </summary>
        /// <param name="obj">要展示的对象</param>
        //public void LoadObjectInformation(NodeModelBase nodeModel)
        public void LoadObjectInformation(object obj)
        {
            if (obj == null)
                return;
            //IsTimerRefres = false;
            //TimerRefreshButton.Content = "定时刷新";
            _objectInstance = obj;
            RefreshObjectTree(obj);
           
        }

        /// <summary>
        /// 添加表达式
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddMonitorExpressionButton_Click(object sender, RoutedEventArgs e)
        {
            //string fullPath = GetNodeFullPath(memberNode);
            //Clipboard.SetDataObject(fullPath);
            OpenInputDialog((exp) =>
            {
                FlowEnvironment.AddInterruptExpression(NodeGuid, exp);

                //if (node.DebugSetting.InterruptExpression.Contains(exp))
                //{
                //    Console.WriteLine("表达式已存在");
                //}
                //else
                //{
                //    node.DebugSetting.InterruptExpression.Add(exp);
                //}
            });
        }


        /// <summary>
        /// 刷新对象属性树
        /// </summary>
        public void RefreshObjectTree(object obj)
        {
            if (obj is null)
                return;
            // _objectInstance = obj;
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
        }









        /// <summary>
        /// 添加父节点
        /// </summary>
        /// <param name="node"></param>
        private static void AddPlaceholderNode(TreeViewItem node)
        {
            node.Items.Add(new TreeViewItem { Header = "Loading..." });
        }

        /// <summary>
        /// 展开子项事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            var item = (TreeViewItem)sender;

            if (item.Items.Count == 1 && item.Items[0] is TreeViewItem placeholder && placeholder.Header.ToString() == "Loading...")
            {
                item.Items.Clear();
                if (item.Tag is FlowDataDetails flowDataDetails) // FlowDataDetails flowDataDetails  object obj
                {
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
        private static void AddMembersToTreeNode(TreeViewItem treeViewNode, object obj, Type type)
        {
            // 获取属性和字段
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var member in members)
            {
                TreeViewItem memberNode = ConfigureTreeViewItem(obj, member);
                treeViewNode.Items.Add(memberNode);

                //if (ConfigureTreeItemMenu(memberNode, member,  out ContextMenu? contextMenu))
                //{
                //    memberNode.ContextMenu = contextMenu; // 设置子项节点的事件
                //}

            }
        }

        /// <summary>
        /// 配置右键菜单功能
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="member"></param>
        /// <returns></returns>
        private static TreeViewItem ConfigureTreeViewItem(object obj, MemberInfo member)
        {
            TreeViewItem memberNode = new TreeViewItem { Header = member.Name };

            if (member is PropertyInfo property)
            {
                FlowDataDetails flowDataDetails = new FlowDataDetails
                {
                    ItemType = TreeItemType.Property,
                    DataType = property.PropertyType,
                    Name = property.Name,
                    DataValue = property,
                };

                memberNode.Tag = flowDataDetails;

                string propertyValue = GetPropertyValue(obj, property);
                memberNode.Header = $"{property.Name} : {property.PropertyType.Name} = {propertyValue}";

                if (!property.PropertyType.IsPrimitive && property.PropertyType != typeof(string))
                {
                    AddPlaceholderNode(memberNode);
                    memberNode.Expanded += TreeViewItem_Expanded;
                }
            }
            else if (member is FieldInfo field)
            {
                FlowDataDetails flowDataDetails = new FlowDataDetails
                {
                    ItemType = TreeItemType.Field,
                    DataType = field.FieldType,
                    Name = field.Name,
                    DataValue = field,
                };

                memberNode.Tag = flowDataDetails;


                string fieldValue = GetFieldValue(obj, field);
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
        private static string GetPropertyValue(object obj, PropertyInfo property)
        {
            try
            {
                var value = property.GetValue(obj);
                return value?.ToString() ?? "null";
            }
            catch
            {
                return "Error";
            }
        }


        /// <summary>
        /// 获取字段类型的成员
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="field"></param>
        /// <returns></returns>
        private static string GetFieldValue(object obj, FieldInfo field)
        {
            try
            {
                var value = field.GetValue(obj);
                return value?.ToString() ?? "null";
            }
            catch
            {
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
        private static bool ConfigureTreeItemMenu(TreeViewItem memberNode, MemberInfo member, out ContextMenu? contextMenu)
        {
            bool isChange = false;
            if (member is PropertyInfo property)
            {
                //isChange = true;
                contextMenu = new ContextMenu();
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
                    string fullPath = ObjectViewerControl.GetNodeFullPath(memberNode);
                    string copyValue = "@Get " + fullPath;
                    Clipboard.SetDataObject(copyValue);
                }));
                //contextMenu.Items.Add(MainWindow.CreateMenuItem($"监视中断", (s, e) =>
                //{
                //    string fullPath = GetNodeFullPath(memberNode);
                //    Clipboard.SetDataObject(fullPath);
                //    OpenInputDialog((exp) =>
                //    {
                //        if (node.DebugSetting.InterruptExpression.Contains(exp))
                //        {
                //            Console.WriteLine("表达式已存在");
                //        }
                //        else
                //        {
                //            node.DebugSetting.InterruptExpression.Add(exp);
                //        }
                //    });

                //}));
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
        private static string GetNodeFullPath(TreeViewItem node)
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
        private static TreeViewItem GetParentTreeViewItem(TreeViewItem node)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(node);
            while (parent != null && !(parent is TreeViewItem))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as TreeViewItem;
        }



        private static InputDialog OpenInputDialog(Action<string> action)
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


