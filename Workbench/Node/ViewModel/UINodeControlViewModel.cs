using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Serein.Workbench.Node.ViewModel
{
    public class UINodeControlViewModel : NodeControlViewModelBase
    {
        private SingleUINode NodeModel => (SingleUINode)base.NodeModel;
        //public IEmbeddedContent Adapter => NodeModel.Adapter;

        public UINodeControlViewModel(NodeModelBase nodeModel) : base(nodeModel)
        {
            //NodeModel.Adapter.GetWindowHandle();
        }

        public void InitAdapter(Action<UserControl> setUIDisplayHandle)
        {
            Task.Factory.StartNew(async () =>
            {
                var context = new DynamicContext(NodeModel.Env);
                await NodeModel.ExecutingAsync(context);
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
