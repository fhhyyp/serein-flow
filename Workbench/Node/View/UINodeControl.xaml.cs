using Serein.Workbench.Node.ViewModel;
using Serein.Workbench.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// UINodeControl.xaml 的交互逻辑
    /// </summary>
    public partial class UINodeControl : NodeControlBase, INodeJunction
    {
        public UINodeControl()
        {
            base.ViewModel.IsEnabledOnView = true;
            InitializeComponent();
        }

        public UINodeControl(UINodeControlViewModel viewModel) : base(viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();


        }



        public JunctionControlBase ExecuteJunction => this.ExecuteJunctionControl;

        public JunctionControlBase NextStepJunction => throw new NotImplementedException();

        public JunctionControlBase[] ArgDataJunction => throw new NotImplementedException();

        public JunctionControlBase ReturnDataJunction => throw new NotImplementedException();


        private void NodeControlBase_Loaded(object sender, RoutedEventArgs e)
        {
            UINodeControlViewModel vm = (UINodeControlViewModel)DataContext;
            vm.InitAdapter(userControl => {
                EmbedContainer.Child = userControl;
            });

            
        }

        private void NodeControlBase_Initialized(object sender, EventArgs e)
        {
            UINodeControlViewModel vm = (UINodeControlViewModel)DataContext;
            
        }
    }
}
