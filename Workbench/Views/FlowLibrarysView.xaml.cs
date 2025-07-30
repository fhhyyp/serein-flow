using Serein.Workbench.Node.ViewModel;
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
        private FlowLibrarysViewModel ViewModel => DataContext as FlowLibrarysViewModel ?? throw new ArgumentNullException();

        /// <summary>
        /// FlowLibrarysView 构造函数
        /// </summary>
        public FlowLibrarysView()
        {
            this.DataContext = App.GetService<Locator>().FlowLibrarysViewModel;
            InitializeComponent();
        }

        private void FlowLibrarysView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (file.EndsWith(".dll"))
                    {
                        ViewModel.LoadFileLibrary(file);
                    }
                }
            }
        }

        private void FlowLibrarysView_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }
}
