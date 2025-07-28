using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.Api;
using Serein.Workbench.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

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
        private readonly IKeyEventService keyEventService;
        private readonly FlowNodeService flowNodeService;

        /// <summary>
        /// 管理工作台的事件
        /// </summary>
        /// <param name="flowEnvironment"></param>
        /// <param name="flowEEForwardingService"></param>
        /// <param name="keyEventService"></param>
        /// <param name="flowNodeService"></param>
        public WorkbenchEventService(IFlowEnvironment flowEnvironment, 
                                     IFlowEEForwardingService flowEEForwardingService,
                                     IKeyEventService keyEventService,
                                     FlowNodeService flowNodeService)
        {
            this.flowEnvironment = flowEnvironment;
            this.flowEEForwardingService = flowEEForwardingService;
            this.keyEventService = keyEventService;
            this.flowNodeService = flowNodeService;
            InitEvents();
        }

        private void InitEvents()
        {
            flowEEForwardingService.ProjectLoaded += FlowEEForwardingService_OnProjectLoaded;
            flowEEForwardingService.ProjectSaving += SaveProjectToLocalFile;
            flowEEForwardingService.EnvOutput += FlowEEForwardingService_OnEnvOut;
            keyEventService.OnKeyDown += KeyEventService_OnKeyDown; ;
        }

        private void FlowEEForwardingService_OnProjectLoaded(ProjectLoadedEventArgs eventArgs)
        {
            var edit = App.GetService<Locator>().FlowEditViewModel;

            App.UIContextOperation.Invoke(async () => {

                foreach (var item in flowNodeService.FlowCanvass)
                {
                    await Task.Delay(50);
                    flowNodeService.CurrentSelectCanvas = item;
                    var tab = edit.CanvasTabs.First(tab => tab.Content == item);
                    edit.SelectedTab = tab;
                }
            });

            
        }

        private void KeyEventService_OnKeyDown(System.Windows.Input.Key key)
        {
            
        }

        private void FlowEEForwardingService_OnEnvOut(InfoType type, string value)
        {
            LogWindow.Instance.AppendText($"{DateTime.Now} [{type}] : {value}{Environment.NewLine}");
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

            #region 获取保存路径
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
            #endregion


            #region 将Dll输出到指定路径
            Uri saveProjectFileUri = new Uri(savePath);
            SereinEnv.WriteLine(InfoType.INFO, "项目文件保存路径：" + savePath);
            for (int index = 0; index < project.Librarys.Length; index++)
            {
                FlowLibraryInfo? library = project.Librarys[index];
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
            #endregion

            #region 输出项目保存文件
            JObject projectJsonData = JObject.FromObject(project);
            File.WriteAllText(savePath, projectJsonData.ToString());
            #endregion

            
        }




    }
    
}


#region 抽取重复文件（例如net运行时）
/*
 
 1. 扫描目录并计算哈希
string[] directories = new[] { "path1", "path2" };
var fileHashMap = new Dictionary<string, List<string>>(); // hash -> List<full paths>

foreach (var dir in directories)
{
    foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
    {
        using var stream = File.OpenRead(file);
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(stream));

        if (!fileHashMap.ContainsKey(hash))
            fileHashMap[hash] = new List<string>();

        fileHashMap[hash].Add(file);
    }
}

2. 将重复文件压缩并保存
string archiveDir = "compressed_output";
Directory.CreateDirectory(archiveDir);

var manifest = new List<FileRecord>();

foreach (var kvp in fileHashMap.Where(kvp => kvp.Value.Count > 1))
{
    var hash = kvp.Key;
    var originalFile = kvp.Value[0];
    var archivePath = Path.Combine(archiveDir, $"{hash}.gz");

    using (var input = File.OpenRead(originalFile))
    using (var output = File.Create(archivePath))
    using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
    {
        input.CopyTo(gzip);
    }

    manifest.Add(new FileRecord
    {
        Hash = hash,
        ArchiveFile = $"{hash}.gz",
        OriginalPaths = kvp.Value
    });
}

3. 生成清单文件（JSON）
public class FileRecord
{
    public string Hash { get; set; }
    public string ArchiveFile { get; set; }
    public List<string> OriginalPaths { get; set; }
}

File.WriteAllText("manifest.json", JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));


4. 根据清单还原原始文件结构
var manifestJson = File.ReadAllText("manifest.json");
var manifest = JsonSerializer.Deserialize<List<FileRecord>>(manifestJson);

foreach (var record in manifest)
{
    var archivePath = Path.Combine("compressed_output", record.ArchiveFile);

    foreach (var path in record.OriginalPaths)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var input = File.OpenRead(archivePath);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = File.Create(path);

        gzip.CopyTo(output);
    }
}
 
 */
#endregion

