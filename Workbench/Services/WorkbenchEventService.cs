using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Services
{

    #region 工作台事件

    public delegate void PreviewlMethodInfoHandler(PreviewlMethodInfoEventArgs eventArgs);

    #endregion

    #region 工作台事件参数
    public class PreviewlMethodInfoEventArgs(MethodDetailsInfo mdInfo) : EventArgs
    {
        /// <summary>
        /// 方法信息
        /// </summary>
        public MethodDetailsInfo MethodDetailsInfo { get; } = mdInfo;
    }
    #endregion


    /// <summary>
    /// 工作台事件管理
    /// </summary>
    internal interface IWorkbenchEventService
    {
        /// <summary>
        /// 预览了某个方法信息（待创建）
        /// </summary>
        event PreviewlMethodInfoHandler OnPreviewlMethodInfo;

        /// <summary>
        /// 预览依赖方法信息
        /// </summary>
        void PreviewLibraryMethodInfo(MethodDetailsInfo mdInfo);
    }

    /// <summary>
    /// 工作台事件的实现类
    /// </summary>
    internal class WorkbenchEventService : IWorkbenchEventService
    {

        private readonly IFlowEnvironment flowEnvironment;
        private readonly IFlowEEForwardingService flowEEForwardingService;

        /// <summary>
        /// 管理工作台的事件
        /// </summary>
        /// <param name="flowEnvironment"></param>
        /// <param name="flowEEForwardingService"></param>
        public WorkbenchEventService(IFlowEnvironment flowEnvironment, IFlowEEForwardingService flowEEForwardingService)
        {
            this.flowEnvironment = flowEnvironment;
            this.flowEEForwardingService = flowEEForwardingService;
            InitEvents();
        }

        private void InitEvents()
        {
            flowEEForwardingService.OnProjectSaving += SaveProjectToLocalFile;
        }

        

        /// <summary>
        /// 预览了某个方法信息（待创建）
        /// </summary>
        public event PreviewlMethodInfoHandler? OnPreviewlMethodInfo;

        /// <summary>
        /// 预览依赖方法信息
        /// </summary>
        public void PreviewLibraryMethodInfo(MethodDetailsInfo mdInfo)
        {
            OnPreviewlMethodInfo?.Invoke(new PreviewlMethodInfoEventArgs(mdInfo));
        }

        /// <summary>
        /// 需要放置节点控件
        /// </summary>
        public void PlateNodeControl()
        {

        }

        /// <summary>
        /// 保存项目数据到本地文件
        /// </summary>
        /// <param name="e"></param>
        private void SaveProjectToLocalFile(ProjectSavingEventArgs e)
        {
            var project = e.ProjectData;
            // 创建一个新的保存文件对话框
            SaveFileDialog saveFileDialog = new()
            {
                Filter = "DynamicNodeFlow Files (*.dnf)|*.dnf",
                DefaultExt = "dnf",
                FileName = "project.dnf"
                // FileName = System.IO.Path.GetFileName(App.FileDataPath)
            };

            // 显示保存文件对话框
            bool? result = saveFileDialog.ShowDialog();
            // 如果用户选择了文件并点击了保存按钮
            if (result == false)
            {
                SereinEnv.WriteLine(InfoType.ERROR, "取消保存文件");
                return;
            }

            var savePath = saveFileDialog.FileName;
            string? librarySavePath = System.IO.Path.GetDirectoryName(savePath);
            if (string.IsNullOrEmpty(librarySavePath))
            {
                SereinEnv.WriteLine(InfoType.ERROR, "保存项目DLL时返回了意外的文件保存路径");
                return;
            }


            Uri saveProjectFileUri = new Uri(savePath);
            SereinEnv.WriteLine(InfoType.INFO, "项目文件保存路径：" + savePath);
            for (int index = 0; index < project.Librarys.Length; index++)
            {
                NodeLibraryInfo? library = project.Librarys[index];
                string sourceFilePath = new Uri(library.FilePath).LocalPath; // 源文件夹
                string targetDir = System.IO.Path.Combine(librarySavePath, library.AssemblyName); // 目标文件夹
                if (!Path.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
                string targetFilePath = System.IO.Path.Combine(targetDir, library.FileName); // 目标文件夹

                try
                {
                    if (File.Exists(sourceFilePath))
                    {
                        if (!File.Exists(targetFilePath))
                        {
                            SereinEnv.WriteLine(InfoType.INFO, $"源文件路径   : {sourceFilePath}");
                            SereinEnv.WriteLine(InfoType.INFO, $"目标路径 : {targetFilePath}");
                            File.Copy(sourceFilePath, targetFilePath, true);

                        }
                        else
                        {
                            SereinEnv.WriteLine(InfoType.WARN, $"目标路径已有类库文件: {targetFilePath}");
                        }
                    }
                    else
                    {
                        SereinEnv.WriteLine(InfoType.WARN, $"源文件不存在 : {targetFilePath}");
                    }
                }
                catch (IOException ex)
                {

                    SereinEnv.WriteLine(InfoType.ERROR, ex.Message);
                }
                var dirName = System.IO.Path.GetDirectoryName(targetFilePath);
                if (!string.IsNullOrEmpty(dirName))
                {
                    var tmpUri2 = new Uri(targetFilePath);
                    var relativePath = saveProjectFileUri.MakeRelativeUri(tmpUri2).ToString(); // 转为类库的相对文件路径

                    //string relativePath = System.IO.Path.GetRelativePath(savePath, targetPath);
                    project.Librarys[index].FilePath = relativePath;
                }

            }

            JObject projectJsonData = JObject.FromObject(project);
            File.WriteAllText(savePath, projectJsonData.ToString());

        }
    }
    
}

