using Serein.Library.Api;
using Serein.Library.Utils;
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

namespace Serein.WorkBench.Themes
{
    /// <summary>
    /// IOCObjectViewControl.xaml 的交互逻辑
    /// </summary>
    public partial class IOCObjectViewControl : UserControl
    {
        private IOCObjectViewMoel IOCObjectViewMoel;
        private SereinIOC sereinIOC;
        public void SetIOC(SereinIOC sereinIOC)
        {
            this.sereinIOC = sereinIOC;
        }

        public IOCObjectViewControl()
        {
            InitializeComponent();
            IOCObjectViewMoel = new IOCObjectViewMoel();
            DataContext = IOCObjectViewMoel;
        }

    }
}
