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
    public class FlowProjectService
    {
        private readonly IFlowEnvironment flowEnvironment;


        public FlowProjectService(IFlowEnvironment flowEnvironment)
        {
            this.flowEnvironment = flowEnvironment;
        }

        public void StartProjectManagementServer()
        {
            // CollabrationSideManagement
        }

        public void LoadLocalProject(string filePath)
        {
            if (File.Exists(filePath))
            {
               /*
                var dir = Path.GetDirectoryName(filePath);
                var flowEnvInfo = new FlowEnvInfo
                {
                    Project = FlowProjectData,
                };*/
                flowEnvironment.LoadProject(filePath);
            }
        }

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
