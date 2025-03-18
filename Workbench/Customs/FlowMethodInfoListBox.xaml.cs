using Serein.Library;
using Serein.Workbench.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace Serein.Workbench.Customs
{


    public class Test: DependencyObject
    {

    }

    /// <summary>
    /// FlowMethodInfoListBox.xaml 的交互逻辑
    /// </summary>
    public partial class FlowMethodInfoListBox :  UserControl
    {


        public FlowMethodInfoListBox()
        {
            this.DataContext = this;
            InitializeComponent();
        }


        public IEnumerable<FlowLibraryMethodDetailsInfo> Nodes
        {
            get { return (IEnumerable<FlowLibraryMethodDetailsInfo>)GetValue(NodesProperty); }
            set { SetValue(NodesProperty, value); }
        }

        //public ItemCollection Items
        //{
        //    get
        //    {
        //        return (ItemCollection)GetValue(ItemsProperty);
        //    }
        //    set
        //    {
        //        SetValue(ItemsProperty, value);
        //    }
        //}


         public static readonly DependencyProperty NodesProperty = DependencyProperty.Register("NodesProperty", typeof(IEnumerable<FlowLibraryMethodDetailsInfo>), typeof(FlowMethodInfoListBox));
        
        //public int TurnValue
        //{
        //    get
        //    {
        //        return (int)GetValue(TurnValueProperty);
        //    }
        //    set
        //    {
        //        SetValue(TurnValueProperty, value);
        //    }

        //}


        // public static readonly DependencyProperty NodesProperty = DependencyProperty.Register(nameof(Nodes), typeof(IEnumerable<FlowLibraryMethodDetailsInfo>), typeof(FlowMethodInfoListBox), new PropertyMetadata(null));

        public Brush BackgroundColor
        {
            get { return (Brush)GetValue(BackgroundColorProperty); }
            set { SetValue(BackgroundColorProperty, value); }
        }

        public static readonly DependencyProperty BackgroundColorProperty =
            DependencyProperty.Register(nameof(BackgroundColor), typeof(Brush), typeof(FlowMethodInfoListBox), new PropertyMetadata(Brushes.White));











    }
}
