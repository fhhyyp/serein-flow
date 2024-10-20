using System.ComponentModel;
using Serein.Library;
using System.Runtime.CompilerServices;

namespace Serein.Workbench.Node.ViewModel
{
    public abstract class NodeControlViewModelBase : INotifyPropertyChanged
    {
        public NodeControlViewModelBase(NodeModelBase node)
        {
            Node = node;
            MethodDetails = Node.MethodDetails;
        }

        /// <summary>
        /// 对应的节点实体类
        /// </summary>
        internal NodeModelBase Node { get; }


        private bool isSelect;
        /// <summary>
        /// 表示节点控件是否被选中
        /// </summary>
        internal bool IsSelect
        {
            get => isSelect; 
            set
            {
                isSelect = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 使节点获得中断能力（以及是否启用节点）
        /// </summary>
        public NodeDebugSetting DebugSetting
        {
            get => Node.DebugSetting;
            set
            {
                if (value != null)
                {
                    Node.DebugSetting = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 使节点能够表达方法信息
        /// </summary>
        public MethodDetails MethodDetails
        {
            get => Node.MethodDetails;
            set
            {
                if(value != null)
                {
                    Node.MethodDetails = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool isInterrupt;
        /// <summary>
        /// 控制中断状态的视觉效果
        /// </summary>
        public bool IsInterrupt
        {
            get => isInterrupt;
            set
            {
                isInterrupt = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        
        /// <summary>
        /// 
        /// </summary>
        public void Selected()
        {
            IsSelect = true;
        }
        /// <summary>
        /// 
        /// </summary>
        public void CancelSelect()
        {
            IsSelect = false;
        }


    }
}
