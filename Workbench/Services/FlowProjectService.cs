using Newtonsoft.Json;
using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Env;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Services
{
    /// <summary>
    /// 流程项目服务
    /// </summary>
    public class FlowProjectService
    {
        private readonly IFlowEnvironment flowEnvironment;


        /// <summary>
        /// 流程项目服务
        /// </summary>
        /// <param name="flowEnvironment"></param>
        public FlowProjectService(IFlowEnvironment flowEnvironment)
        {
            this.flowEnvironment = flowEnvironment;
        }

        /// <summary>
        /// 启动流程项目管理服务器
        /// </summary>
        public void StartProjectManagementServer()
        {
            // CollabrationSideManagement
        }

        /// <summary>
        /// 加载本地流程项目到当前环境中
        /// </summary>
        /// <param name="filePath"></param>
        public void LoadLocalProject(string filePath)
        {
            if (File.Exists(filePath))
            {
                flowEnvironment.LoadProject(filePath);
            }
        }

        /// <summary>
        /// 选择本地流程项目文件并加载到当前环境中
        /// </summary>
        public void SelectProjectFile()
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "流程项目文件|*.dnf|所有文件|*.*";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            openFileDialog.Title = "打开项目文件";
            openFileDialog.Multiselect = false;

            // 显示文件对话框
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // 获取用户选择的文件路径
                var projectFile = openFileDialog.FileName;
                LoadLocalProject(projectFile);
            }
        }

    }
}
