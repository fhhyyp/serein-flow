using Serein.Library.Attributes;
using Serein.Library.Entity;
using Serein.Library.Utils;
using Serein.NodeFlow;
using Serein.NodeFlow.Tool;
using Serein.WorkBench.Node.View;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Serein.WorkBench
{
    public class MainWindowViewModel
    {
        private readonly MainWindow window ;
        public MainWindowViewModel(MainWindow window)
        {
            FlowEnvironment = new FlowEnvironment();
            this.window = window;
        }

        public FlowEnvironment FlowEnvironment { get; set; }


        #region 加载项目文件
        public void LoadProjectFile(SereinOutputFileData projectFile)
        {
            var dllPaths = projectFile.Librarys.Select(it => it.Path).ToList();
            foreach (var dll in dllPaths)
            {
                var filePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(App.FileDataPath, dll));
                //LoadAssembly(filePath);
            }
        }





        private void DisplayControlDll(Assembly assembly,
                                List<MethodDetails> conditionTypes,
                                List<MethodDetails> actionTypes,
                                List<MethodDetails> flipflopMethods)
        {

            var dllControl = new DllControl
            {
                Header = "DLL name :  " + assembly.GetName().Name // 设置控件标题为程序集名称
            };


            foreach (var item in actionTypes)
            {
                dllControl.AddAction(item.Clone());  // 添加动作类型到控件
            }
            foreach (var item in flipflopMethods)
            {
                dllControl.AddFlipflop(item.Clone());  // 添加触发器方法到控件
            }

            /*foreach (var item in stateTypes)
            {
                dllControl.AddState(item);
            }*/

            window.DllStackPanel.Children.Add(dllControl);  // 将控件添加到界面上显示
        }



        #endregion


       
    }
}
