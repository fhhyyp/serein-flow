using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model;
using Serein.Workbench.Node.ViewModel;
using System.Windows;
using System.Windows.Controls;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// FlowCallNodeControl.xaml 的交互逻辑
    /// </summary>
    public partial class FlowCallNodeControl : NodeControlBase, INodeJunction
    {
        private new FlowCallNodeControlViewModel ViewModel { get; set; }
        public FlowCallNodeControl()
        {
            var env = App.GetService<IFlowEnvironment>();
            base.ViewModel = new FlowCallNodeControlViewModel(new SingleFlowCallNode(env));
            base.ViewModel.IsEnabledOnView = false;
            DataContext = base.ViewModel;
            InitializeComponent();
        }
        public FlowCallNodeControl(FlowCallNodeControlViewModel viewModel) : base(viewModel)
        {
            DataContext = viewModel;
            ViewModel = viewModel;
            InitializeComponent();
            ViewModel.UploadMethodDetailsControl = UploadMethodDetailsControl;

        }

        private void UploadMethodDetailsControl(MethodDetails methodDetails)
        {
            //MethodDetailsControl.MethodDetails = methodDetails;

        }

        /// <summary>
        /// 入参控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.ExecuteJunction => this.ExecuteJunctionControl;

        /// <summary>
        /// 下一个调用方法控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.NextStepJunction => throw new NotImplementedException("不存在下一个调用控制点");

        /// <summary>
        /// 返回值控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.ReturnDataJunction => throw new NotImplementedException("不存在返回值控制点");


        /// <summary>
        /// 方法入参控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase[] INodeJunction.ArgDataJunction => GetArgJunction();

        private JunctionControlBase[] GetArgJunction()
        {
            // 获取 MethodDetailsControl 实例
            //var methodDetailsControl = ViewModel.NodeModel.IsShareParam ?  this.SelectMethodDetailsControl : this.MyMethodDetailsControl;
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
            return [];
        }


    }
}
