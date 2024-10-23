using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.Node.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Serein.Workbench.Node.View
{

    /// <summary>
    /// 节点控件基类（控件）
    /// </summary>
    public abstract class NodeControlBase : UserControl, IDynamicFlowNode
    {
        public NodeControlViewModelBase ViewModel { get; set; }


        protected NodeControlBase()
        {
            this.Background = Brushes.Transparent;
        }
        protected NodeControlBase(NodeControlViewModelBase viewModelBase)
        {
            ViewModel = viewModelBase;
            this.Background = Brushes.Transparent;
            this.DataContext = viewModelBase;
            SetBinding();
        }


        public void SetBinding()
        {
            // 绑定 Canvas.Left
            Binding leftBinding = new Binding("X")
            {
                Source = ViewModel.NodeModel.Position, // 如果 X 属性在当前 DataContext 中
                Mode = BindingMode.TwoWay
            };
            BindingOperations.SetBinding(this, Canvas.LeftProperty, leftBinding);

            // 绑定 Canvas.Top
            Binding topBinding = new Binding("Y")
            {
                Source = ViewModel.NodeModel.Position, // 如果 Y 属性在当前 DataContext 中
                Mode = BindingMode.TwoWay
            };
            BindingOperations.SetBinding(this, Canvas.TopProperty, topBinding);
        }

        



    }



 
    //public class FLowNodeObObservableCollection<T> : ObservableCollection<T>
    //{

    //    public void AddRange(IEnumerable<T> items)
    //    {
    //        foreach (var item in items)
    //        {
    //            this.Items.Add(item);
    //        }
    //        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add));
    //    }
    //}
}






