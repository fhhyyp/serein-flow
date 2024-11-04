using Serein.NodeFlow.Model;
using Serein.Workbench.Node.ViewModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// ActionNode.xaml 的交互逻辑
    /// </summary>
    public partial class ActionNodeControl : NodeControlBase, INodeJunction
    {
        public ActionNodeControl(ActionNodeControlViewModel viewModel) : base(viewModel) 
        {
            DataContext = viewModel;
            InitializeComponent();
            ExecuteJunctionControl.MyNode.Guid = viewModel.NodeModel.Guid;
        }

        /// <summary>
        /// 入参控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.ExecuteJunction => this.ExecuteJunctionControl;

        /// <summary>
        /// 下一个调用方法控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.NextStepJunction => this.NextStepJunctionControl;

        /// <summary>
        /// 返回值控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.ReturnDataJunction => this.ResultJunctionControl;

        /// <summary>
        /// 方法入参控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase[] INodeJunction.ArgDataJunction
        {
            get
            {
                // 获取 MethodDetailsControl 实例
                var methodDetailsControl = this.MethodDetailsControl;
                var itemsControl = FindVisualChild<ItemsControl>(methodDetailsControl); // 查找 ItemsControl
                if (itemsControl != null)
                {
                    var argDataJunction = new JunctionControlBase[base.ViewModel.NodeModel.MethodDetails.ParameterDetailss.Length];
                    var controls = new List<JunctionControlBase>();

                    for (int i = 0; i < itemsControl.Items.Count; i++)
                    {
                        var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                        if (container != null)
                        {
                            var argControl = FindVisualChild<ArgJunctionControl>(container);
                            if (argControl != null)
                            {
                                controls.Add(argControl); // 收集 ArgJunctionControl 实例
                            }
                        }
                    }
                    return argDataJunction = controls.ToArray();
                }
                else
                {
                    return [];
                }
            }
        }




    }
}
