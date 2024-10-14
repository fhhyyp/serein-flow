using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.NodeFlow.Base;
using Serein.WorkBench.Node.ViewModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Media;

namespace Serein.WorkBench.Node.View
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
        }
    }



  





    public class FLowNodeObObservableCollection<T> : ObservableCollection<T>
    {

        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                this.Items.Add(item);
            }
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add));
        }
    }
}






