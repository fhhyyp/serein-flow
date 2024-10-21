using System.ComponentModel;
using Serein.Library;
using System.Runtime.CompilerServices;

namespace Serein.Workbench.Node.ViewModel
{
    public abstract class NodeControlViewModelBase : INotifyPropertyChanged
    {
        public NodeControlViewModelBase(NodeModelBase nodeModel)
        {
            NodeModel = nodeModel;

            // 订阅来自 NodeModel 的通知事件
        }


        private NodeModelBase _nodeModelBase;
        /// <summary>
        /// 对应的节点实体类
        /// </summary>
        public NodeModelBase NodeModel
        {
            get => _nodeModelBase; set
            {
                if (value != null)
                {
                    _nodeModelBase = value;
                    OnPropertyChanged();
                }
            }
        }


        //private bool isSelect;
        ///// <summary>
        ///// 表示节点控件是否被选中
        ///// </summary>
        //internal bool IsSelect
        //{
        //    get => isSelect; 
        //    set
        //    {
        //        isSelect = value;
        //        OnPropertyChanged();
        //    }
        //}



        //private bool isInterrupt;
        /////// <summary>
        /////// 控制中断状态的视觉效果
        /////// </summary>
        //public bool IsInterrupt
        //{
        //    get => NodeModel.DebugSetting.IsInterrupt;
        //    set
        //    {
        //        NodeModel.DebugSetting.IsInterrupt = value;
        //        OnPropertyChanged();
        //    }
        //}

        public event PropertyChangedEventHandler? PropertyChanged;
         protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
         {
             //Console.WriteLine(propertyName);
             PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
         }


        /// <summary>
        /// 使节点获得中断能力（以及是否启用节点）
        /// </summary>
        //public NodeDebugSetting DebugSetting
        //{
        //    get => Node.DebugSetting;
        //    set
        //    {
        //        if (value != null)
        //        {
        //            Node.DebugSetting = value;
        //            OnPropertyChanged();
        //        }
        //    }
        //}

        /// <summary>
        /// 使节点能够表达方法信息
        /// </summary>
        //public MethodDetails MethodDetails
        //{
        //    get => Node.MethodDetails;
        //    set
        //    {
        //        if(value != null)
        //        {
        //            Node.MethodDetails = value;
        //            OnPropertyChanged();
        //        }
        //    }
        //}

    }
}
