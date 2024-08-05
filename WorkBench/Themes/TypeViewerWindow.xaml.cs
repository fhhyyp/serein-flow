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

        private void AddMembersToTreeNode(TreeViewItem node, Type type)
        {
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var member in members)
            {
                var memberNode = new TreeViewItem { Header = member.Name };
                if (member is PropertyInfo property)
                {
                    var propertyType = property.PropertyType;
                    memberNode.Header = $"{member.Name} : {propertyType.Name}";
                    if (!propertyType.IsPrimitive && propertyType != typeof(string))
                    {
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
