﻿using Serein.NodeFlow.Model;
using Serein.Workbench.Node.ViewModel;
using System.Windows.Controls;
using System.Windows;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// StateNode.xaml 的交互逻辑
    /// </summary>
    public partial class FlipflopNodeControl : NodeControlBase, INodeJunction
    {
        public FlipflopNodeControl(FlipflopNodeControlViewModel viewModel) : base(viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
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
        private JunctionControlBase[] argDataJunction;
        /// <summary>
        /// 方法入参控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase[] INodeJunction.ArgDataJunction
        {
            get
            {
                if (argDataJunction == null)
                {
                    // 获取 MethodDetailsControl 实例
                    var methodDetailsControl = this.MethodDetailsControl;
                    argDataJunction = new JunctionControlBase[base.ViewModel.NodeModel.MethodDetails.ParameterDetailss.Length];

                    var itemsControl = FindVisualChild<ItemsControl>(methodDetailsControl); // 查找 ItemsControl
                    if (itemsControl != null)
                    {
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
                        argDataJunction = controls.ToArray();
                    }
                }
                return argDataJunction;
            }
        }
    }
}
