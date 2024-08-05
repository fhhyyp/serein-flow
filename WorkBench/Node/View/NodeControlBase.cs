using Serein.DynamicFlow;
using Serein.DynamicFlow.NodeModel;
using Serein.WorkBench.Themes;
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
        public NodeBase Node { get; set; }

        protected NodeControlBase()

        {
            this.Background = Brushes.Transparent;
        }
        protected NodeControlBase(NodeBase node)
        {
            this.Background = Brushes.Transparent;
            Node = node;
        }
    }



    public abstract class NodeControlViewModel : INotifyPropertyChanged
    {


        public MethodDetails methodDetails;


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






