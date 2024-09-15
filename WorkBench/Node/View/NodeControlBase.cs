using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.NodeFlow.Base;
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



    public abstract class NodeControlViewModelBase : INotifyPropertyChanged
    {
        public NodeControlViewModelBase(NodeModelBase node)
        {
            this.Node = node;
            MethodDetails = this.Node.MethodDetails;
        }

        /// <summary>
        /// 对应的节点实体类
        /// </summary>
        public NodeModelBase Node { get; }

        /// <summary>
        /// 表示节点控件是否被选中
        /// </summary>
        public bool IsSelect { get; set; } = false;

        private MethodDetails methodDetails;


        public MethodDetails MethodDetails
        {
            get => methodDetails;
            set
            {
                methodDetails = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)

        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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






