using Serein.Library.Entity;
using Serein.NodeFlow.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Serein.WorkBench.Node.ViewModel
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
                // OnPropertyChanged();
            }
        }


        public NodeDebugSetting DebugSetting
        {
            get => Node.DebugSetting;
            set
            {
                if (value != null)
                {
                    Node.DebugSetting = value;
                    OnPropertyChanged(/*nameof(DebugSetting)*/);
                }
            }
        }

        public MethodDetails MethodDetails
        {
            get => Node.MethodDetails;
            set
            {
                if(value != null)
                {
                    Node.MethodDetails = value;
                    OnPropertyChanged(/*nameof(MethodDetails)*/);
                }
            }
        }

        private bool isInterrupt;
        public bool IsInterrupt
        {
            get => isInterrupt;
            set
            {
                isInterrupt = value;
                OnPropertyChanged(/*nameof(IsInterrupt)*/);
            }
        }


        //public bool IsInterrupt
        //{
        //    get => Node.DebugSetting.IsInterrupt;
        //    set
        //    {
        //        if (value)
        //        {
        //            Node.Interrupt();
        //        }
        //        else
        //        {
        //            Node.CancelInterrupt();
        //        }
        //        OnPropertyChanged(nameof(IsInterrupt));
        //    }
        //}

        //public bool IsProtectionParameter
        //{
        //    get => MethodDetails.IsProtectionParameter;
        //    set
        //    {
        //        MethodDetails.IsProtectionParameter = value;
        //        OnPropertyChanged(nameof(IsInterrupt));
        //    }
        //}

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        

        public void Selected()
        {
            IsSelect = true;
        }

        public void CancelSelect()
        {
            IsSelect = false;
        }


    }
}
