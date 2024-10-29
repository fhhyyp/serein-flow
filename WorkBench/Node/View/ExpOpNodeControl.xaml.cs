﻿using Serein.NodeFlow.Model;
using Serein.Workbench.Node.ViewModel;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// ExprOpNodeControl.xaml 的交互逻辑
    /// </summary>
    public partial class ExpOpNodeControl : NodeControlBase, INodeJunction
    {
        public ExpOpNodeControl() : base()
        {
            // 窗体初始化需要
            ViewModel = new ExpOpNodeViewModel(new SingleExpOpNode(null));
            DataContext = ViewModel;
            InitializeComponent();
        }
        public ExpOpNodeControl(ExpOpNodeViewModel viewModel) :base(viewModel)
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
                argDataJunction = new JunctionControlBase[1];
                argDataJunction[0] = this.ArgJunctionControl;
                return argDataJunction;
            }
        }
    }
}
