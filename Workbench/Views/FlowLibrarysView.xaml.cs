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
    /// FlowLibrarysView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowLibrarysView : UserControl
    {
        public FlowLibrarysView()
        {
            this.DataContext = App.GetService<Locator>().FlowLibrarysViewModel;
            InitializeComponent();
        }
    }
}
