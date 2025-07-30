using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Serein.Workbench.Node.ViewModel
{
    /// <summary>
    /// UI节点控制器视图模型
    /// </summary>

    public partial class UINodeControlViewModel : NodeControlViewModelBase
    {
        private new SingleUINode NodeModel => (SingleUINode)base.NodeModel;
        //public IEmbeddedContent Adapter => NodeModel.Adapter;

        /// <summary>
        /// 节点UI的对应内容
        /// </summary>
        [ObservableProperty]
        private UserControl _nodeUIContent;


        public UINodeControlViewModel(IFlowNode nodeModel) : base(nodeModel)
        {
            
        }

        public void InitAdapter()
        {
            Task.Factory.StartNew(async () =>
            {
                var context = new FlowContext(NodeModel.Env);
                var cts = new CancellationTokenSource();    
                var result = await NodeModel.ExecutingAsync(context, cts.Token);
                cts?.Dispose();
                if (context.NextOrientation == ConnectionInvokeType.IsSucceed
                        && NodeModel.Adapter.GetUserControl() is UserControl userControl) 
                {
                    NodeModel.Env.UIContextOperation.Invoke(() => 
                    {
                        NodeUIContent = userControl;
                    });
                }
            });
        }


        public void InitAdapter(Action<UserControl> setUIDisplayHandle)
        {
            Task.Factory.StartNew(async () =>
            {
                var context = new FlowContext(NodeModel.Env);
                var cts = new CancellationTokenSource();
                var result = await NodeModel.ExecutingAsync(context, cts.Token);
                cts?.Dispose();
                if (context.NextOrientation == ConnectionInvokeType.IsSucceed
                        && NodeModel.Adapter.GetUserControl() is UserControl userControl)
                {
                    NodeModel.Env.UIContextOperation.Invoke(() =>
                    {
                        setUIDisplayHandle.Invoke(userControl);
                    });
                }
            });
        }

    }
}
