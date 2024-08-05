using Serein.DynamicFlow.NodeModel;
using Serein.WorkBench.Node.ViewModel;
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

namespace Serein.WorkBench.Node.View
{
    /// <summary>
    /// ExprOpNodeControl.xaml 的交互逻辑
    /// </summary>
    public partial class ExpOpNodeControl : NodeControlBase
    {
        public ExpOpNodeViewModel ViewModel { get; }

        public ExpOpNodeControl()
        {
            ViewModel = new (new());
            DataContext = ViewModel;
            InitializeComponent();
        }
        public ExpOpNodeControl(SingleExpOpNode node):base(node)
        {
            Node = node;
            ViewModel = new(node);
            DataContext = ViewModel;
            InitializeComponent();
        }

    }
}
