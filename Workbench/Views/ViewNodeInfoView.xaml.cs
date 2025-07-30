using Serein.Workbench.ViewModels;
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

namespace Serein.Workbench.Views
{
    /// <summary>
    /// NodeInfoView.xaml 的交互逻辑
    /// </summary>
    public partial class ViewNodeInfoView : UserControl
    {
        private readonly ViewNodeInfoViewModel ViewModel;

        /// <summary>
        /// ViewNodeInfoView 的交互逻辑
        /// </summary>
        public ViewNodeInfoView()
        {

            ViewModel = App.GetService<Locator>().NodeInfoViewModel;
            this.DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
