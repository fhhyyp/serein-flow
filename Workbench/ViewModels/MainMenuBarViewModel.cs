using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serein.Library.Api;
using System.Windows.Input;

namespace Serein.Workbench.ViewModels
{
    public class MainMenuBarViewModel : ObservableObject
    {
        private readonly IFlowEnvironment environment;

        /// <summary>
        /// 保存项目
        /// </summary>
        public ICommand SaveProjectCommand { get; private set; }
        /// <summary>
        /// 加载本地文件
        /// </summary>
        public ICommand LoadLocalProjectCommand { get; private set; }
        /// <summary>
        /// 加载远程项目
        /// </summary>
        public ICommand LoadRemoteProjectCommand { get; private set; }
    
        /// <summary>
        /// 增加流程图
        /// </summary>
        public ICommand CreateFlowCanvasCommand { get; private set; }

        /// <summary>
        /// 增加流程图
        /// </summary>
        public ICommand RemoteFlowCanvasCommand { get; private set; }

        
        /// <summary>
        /// 打开环境输出窗口
        /// </summary>
        public ICommand OpenEnvOutWindowCommand { get; private set; }
        
        /// <summary>
        /// 打开动态编译窗口
        /// </summary>
        public ICommand OpenDynamicCompilerCommand { get; private set; }



        public MainMenuBarViewModel(IFlowEnvironment environment)
        {
            this.environment = environment;

            SaveProjectCommand = new RelayCommand(SaveProject);
        }

        public void SaveProject()
        {

        }

    }
}
