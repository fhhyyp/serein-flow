using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia;
using Serein.Library;
using Serein.Workbench.Avalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Input;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Collections;
using System.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;

namespace Serein.Workbench.Avalonia.Custom.ViewModels
{

    internal partial class FlowLibraryInfoViewModel:ViewModelBase
    {
        /// <summary>
        /// 依赖名称
        /// </summary>
        [ObservableProperty]
        public string _libraryName;

        private ObservableCollection<MethodDetailsInfo> activateMethods;
        private ObservableCollection<MethodDetailsInfo> flipflopMethods;


        /// <summary>
        /// 动作节点
        /// </summary>
        public ObservableCollection<MethodDetailsInfo> ActivateMethods { get => activateMethods; set => SetProperty(ref activateMethods,value); }

        /// <summary>
        /// 触发器节点
        /// </summary>
        public ObservableCollection<MethodDetailsInfo> FlipflopMethods { get => flipflopMethods; set => SetProperty(ref activateMethods, value); }


        ///// <summary>
        ///// 加载项目信息
        ///// </summary>
        ///// <param name="libraryMds"></param>
        //public void LoadLibraryInfo(LibraryMds libraryMds)
        //{
        //    this.AssemblyName = libraryMds.AssemblyName;
        //}






    }
}
