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
    /// ObjectViewerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ObjectViewerControl : UserControl
    {
        public ObjectViewerControl()
        {
            InitializeComponent();
        }

        private object _objectInstance;
        private Action _closeCallback;

        public void LoadObjectInformation(object obj,Action closeCallback)
        {
            if (obj == null || closeCallback == null) 
                return;
            _closeCallback = closeCallback;
            _objectInstance = obj;
            var objectType = obj.GetType();
            var rootNode = new TreeViewItem { Header = objectType.Name, Tag = obj };

            // 添加占位符节点
            AddPlaceholderNode(rootNode);
            ObjectTreeView.Items.Clear();
            ObjectTreeView.Items.Add(rootNode);

            // 监听展开事件
            rootNode.Expanded += TreeViewItem_Expanded;
        }

        private void AddPlaceholderNode(TreeViewItem node)
        {
            node.Items.Add(new TreeViewItem { Header = "Loading..." });
        }

        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            var item = (TreeViewItem)sender;

            if (item.Items.Count == 1 && item.Items[0] is TreeViewItem placeholder && placeholder.Header.ToString() == "Loading...")
            {
                item.Items.Clear();
                if (item.Tag is object obj)
                {
                    var objectType = obj.GetType();
                    AddMembersToTreeNode(item, obj, objectType);
                }
            }
        }

        private void AddMembersToTreeNode(TreeViewItem node, object obj, Type type)
        {
            // 获取属性和字段
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var member in members)
            {
                TreeViewItem memberNode = ConfigureTreeViewItem(obj, member);
                node.Items.Add(memberNode);
            }
        }

        private TreeViewItem ConfigureTreeViewItem(object obj, MemberInfo member)
        {
            TreeViewItem memberNode = new TreeViewItem { Header = member.Name };

            if (member is PropertyInfo property)
            {
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

        private string GetPropertyValue(object obj, PropertyInfo property)
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

        private string GetFieldValue(object obj, FieldInfo field)
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

        private void Window_Closed(object sender, EventArgs e)
        {
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

            _closeCallback?.Invoke();
        }
    }
}
