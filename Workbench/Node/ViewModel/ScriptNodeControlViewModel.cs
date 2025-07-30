using Serein.Library;
using Serein.Library.Utils;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Serein.Workbench.Node.ViewModel
{
    /// <summary>
    /// 脚本节点控制视图模型
    /// </summary>
    public class ScriptNodeControlViewModel : NodeControlViewModelBase
    {

        private new SingleScriptNode NodeModel => (SingleScriptNode)base.NodeModel;

        public string? Script
        {
            get => NodeModel?.Script;
            set { NodeModel.Script = value; OnPropertyChanged(); }
        }



        public ScriptNodeControlViewModel(NodeModelBase nodeModel) : base(nodeModel)
        {
            CommandExecuting = new RelayCommand(async o =>
            {
                try
                {
                    var cts = new CancellationTokenSource(); 
                    var result = await NodeModel.ExecutingAsync(new Library.FlowContext(nodeModel.Env), cts.Token);
                    var data = result.Value;
                    cts.Cancel();
                    SereinEnv.WriteLine(InfoType.INFO, data?.ToString());
                }
                catch (Exception ex)
                {
                    SereinEnv.WriteLine(ex);
                }
            });

            CommandLoadScript = new RelayCommand( o =>
            {
                NodeModel.ReloadScript(); // 工作台重新加载脚本
            });

            CommandGenerateCode =  new RelayCommand(o =>
            {
                var info = NodeModel.ToCsharpMethodInfo($"Test_{nodeModel.Guid.Replace("-","_")}"); // 工作台重新加载脚本
                if (info is null) return;
                SereinEnv.WriteLine(InfoType.INFO, $"{info.ClassName}.{info.MethodName}({string.Join(",", info.ParamInfos.Select(i => $"global::{i.ParameterType.FullName} {i.ParamName}"))})");
                SereinEnv.WriteLine(InfoType.INFO, info.CsharpCode);
            });
        }


        /// <summary>
        /// 加载脚本代码
        /// </summary>
        public ICommand CommandLoadScript{ get; }

        /// <summary>
        /// 尝试执行
        /// </summary>
        public ICommand CommandExecuting { get; }

        /// <summary>
        /// 生成c#代码
        /// </summary>
        public ICommand CommandGenerateCode { get; }



    }
}
