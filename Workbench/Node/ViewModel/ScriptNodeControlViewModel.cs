using Serein.Library;
using Serein.Library.Core;
using Serein.Library.Utils;
using Serein.NodeFlow.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Serein.Workbench.Node.ViewModel
{
    public class ScriptNodeControlViewModel : NodeControlViewModelBase
    {
        private SingleScriptNode NodeModel => (SingleScriptNode)base.NodeModel;

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
                    var result = await NodeModel.ExecutingAsync(new DynamicContext(nodeModel.Env));
                    SereinEnv.WriteLine(InfoType.INFO, result?.ToString());
                }
                catch (Exception ex)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, ex.ToString());
                }
            });

            CommandLoadScript = new RelayCommand( o =>
            {
                NodeModel.LoadScript();
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



    }
}
