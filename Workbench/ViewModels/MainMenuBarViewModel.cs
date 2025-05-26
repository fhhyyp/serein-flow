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
        /// 移除流程图
        /// </summary>
        public ICommand RemoteFlowCanvasCommand { get; private set; }

        /// <summary>
        /// 运行当前画布流程
        /// </summary>
        public ICommand StartCurrentCanvasFlowCommand { get; private set; }

        /// <summary>
        /// 停止当前画布流程
        /// </summary>
        public ICommand StopCurrentCanvasFlowCommand { get; private set; }



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

            SaveProjectCommand = new RelayCommand(SaveProject); // 保存项目
            LoadLocalProjectCommand = new RelayCommand(LoadLocalProject); // 加载本地项目
            LoadRemoteProjectCommand = new RelayCommand(LoadRemoteProject); // 加载远程项目

            CreateFlowCanvasCommand = new RelayCommand(CreateFlowCanvas); // 增加画布
            RemoteFlowCanvasCommand = new RelayCommand(RemoteFlowCanvas); // 移除画布

            StartCurrentCanvasFlowCommand = new RelayCommand(StartCurrentCanvasFlow); // 运行当前流程
            StopCurrentCanvasFlowCommand = new RelayCommand(StopCurrentCanvasFlow); // 停止当前流程

            OpenEnvOutWindowCommand = new RelayCommand(OpenEnvOutWindow); // 打开运行输出窗口
            OpenDynamicCompilerCommand = new RelayCommand(OpenDynamicCompiler); // 打开动态编译仓库窗口
        }

        private void SaveProject() {
            environment.SaveProject(); // 保存项目
        }
        private void LoadLocalProject() {
            //environment.LoadProject(); // 加载项目
        }
        private void LoadRemoteProject() { }
        private void CreateFlowCanvas() { }
        private void RemoteFlowCanvas() { }
        private void StartCurrentCanvasFlow() { }
        private void StopCurrentCanvasFlow() { }
        private void OpenDynamicCompiler() { }
        private void OpenEnvOutWindow() { }

    }
}
