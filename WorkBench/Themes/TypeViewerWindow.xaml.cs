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

            var rootNode = new TreeViewItem { Header = Type.Name };
            AddMembersToTreeNode(rootNode, Type);
            TypeTreeView.Items.Clear();
            TypeTreeView.Items.Add(rootNode);
        }

        /// <summary>
        /// 添加属性节点
        /// </summary>
        /// <param name="node"></param>
        /// <param name="type"></param>
        private void AddMembersToTreeNode(TreeViewItem node, Type type)
        {
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var member in members)
            {
                TreeViewItem memberNode;
                try
                {
                    memberNode = new TreeViewItem { Header = member.Name };
                }
                catch 
                {
                    return;
                }
                
                if (member is PropertyInfo property)
                {
                    var propertyType = property.PropertyType;
                    memberNode.Header = $"{member.Name} : {propertyType.Name}";
                    if (!propertyType.IsPrimitive && propertyType != typeof(string))
                    {
                        // 递归显示类型属性的节点
                        AddMembersToTreeNode(memberNode, propertyType);
                    }
                }
                else if (member is MethodInfo method)
                {
                    var parameters = method.GetParameters();
                    var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    memberNode.Header = $"{member.Name}({paramStr})";
                }
                else if (member is FieldInfo field)
                {
                    memberNode.Header = $"{member.Name} : {field.FieldType.Name}";
                }

                node.Items.Add(memberNode);
            }
        }
    }
}
