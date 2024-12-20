using System.ComponentModel;
using Serein.Library;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Data;
using System;

namespace Serein.Workbench.Node.ViewModel
{
    public abstract class NodeControlViewModelBase
    {
        ///// <summary>
        ///// 对应的节点实体类
        ///// </summary>
        public NodeModelBase NodeModel { get; }

        public NodeControlViewModelBase(NodeModelBase nodeModel)
        {
            NodeModel = nodeModel;

        }

        
        private bool isInterrupt;
        ///// <summary>
        ///// 控制中断状态的视觉效果
        ///// </summary>
        public bool IsInterrupt
        {
            get => NodeModel.DebugSetting.IsInterrupt;
            set
            {
                NodeModel.DebugSetting.IsInterrupt = value;
                OnPropertyChanged();
            }
        }



        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


    }
}
