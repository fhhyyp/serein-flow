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
        private  new UINodeControlViewModel ViewModel {  get; }
        public UINodeControl()
        {
            base.ViewModel.IsEnabledOnView = true;
            base.ViewModel.NodeModel.DisplayName = "[流程UI]";
            InitializeComponent();
        }

        internal UINodeControl(UINodeControlViewModel viewModel) : base(viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            ViewModel.NodeModel.DisplayName = "[流程UI]";
            InitializeComponent();
        }



       JunctionControlBase INodeJunction.ExecuteJunction => this.ExecuteJunctionControl;

       JunctionControlBase INodeJunction.NextStepJunction => throw new NotImplementedException();

       JunctionControlBase[] INodeJunction.ArgDataJunction => throw new NotImplementedException();

       JunctionControlBase INodeJunction.ReturnDataJunction => throw new NotImplementedException();


        private void NodeControlBase_Loaded(object sender, RoutedEventArgs e)
        {
            //ViewModel.InitAdapter(); 
            ViewModel.InitAdapter(userControl => {
                EmbedContainer.Child = userControl;
            });

        }

        private void NodeControlBase_Initialized(object sender, EventArgs e)
        {
            UINodeControlViewModel vm = (UINodeControlViewModel)DataContext;
            
        }
    }
}
