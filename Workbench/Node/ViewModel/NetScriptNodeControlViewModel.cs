using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Library;
using Serein.NodeFlow.Model.Nodes;
using Serein.Workbench.Themes;
using System.Windows.Input;

namespace Serein.Workbench.Node.ViewModel
{
    /// <summary>
    /// 动态脚本节点控制视图模型
    /// </summary>
    public class NetScriptNodeControlViewModel : NodeControlViewModelBase
    {
        private new SingleNetScriptNode NodeModel => (SingleNetScriptNode)base.NodeModel;

        public string Tips
        {
            get => NodeModel.Tips;
            set { NodeModel.Tips = value; OnPropertyChanged(); }
        }

        public string Script
        {
            get => NodeModel.Script;
            set { NodeModel.Script = value; OnPropertyChanged(); }
        }


        public NetScriptNodeControlViewModel(NodeModelBase nodeModel) : base(nodeModel)
        {
            Script = @"using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

[DynamicFlow(""[动态编译]"")]
public class FlowLibrary
{
	[NodeAction(NodeType.Action, AnotherName = ""输出"")]
    public void Print(IDynamicContext context,string value = ""Hello World!"")
    {
        context.Env.WriteLine(InfoType.INFO, value);
    }
}";

            CommandOpenScriptEdit = new RelayCommand(o =>
            {
                DynamicCompilerView dynamicCompilerView = new DynamicCompilerView();
                dynamicCompilerView.ScriptCode = this.Script ;
                dynamicCompilerView.OnCompileComplete = OnCompileComplete;
                dynamicCompilerView.ShowDialog();
            });
            NodeModel1 = nodeModel;
        }

        private static void OnCompileComplete(FlowLibraryCache flowLibrary)
        {
            var loadResult = flowLibrary.LoadFlowMethod(); // 动态编译完成后加载程序集
            if (!loadResult)
            {
                return ;
            }

            var md = flowLibrary.MethodDetailss.Values.FirstOrDefault();
            if (md is null)
            {
                return;
            }
            

        }

        /// <summary>
        /// 打开编辑窗口
        /// </summary>
        public ICommand CommandOpenScriptEdit { get; }

        /// <summary>
        /// 节点模型
        /// </summary>
        public NodeModelBase NodeModel1 { get; }
    }
}
