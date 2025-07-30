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
    /// MainView.xaml 的交互逻辑
    /// </summary>
    public partial class MainView : UserControl
    {
        /// <summary>
        /// MainView 的构造函数
        /// </summary>
        public MainView()
        {
            this.DataContext = App.GetService<Locator>().MainViewModel;
            InitializeComponent();

            Window window = new System.Windows.Window();
            window.Show();
        }
    }
}
