using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Models
{

    /// <summary>
    /// 依赖信息
    /// </summary>
    internal partial class FlowLibraryInfo : ObservableObject
    {
        [ObservableProperty]
        private string _filePath;

        [ObservableProperty]
        private string _libraryName;

        [ObservableProperty]
        private ObservableCollection<MethodDetailsInfo> _methodInfo;


        public List<MethodDetailsInfo> ActionNodes { get =>  MethodInfo.Where(x => x.NodeType == NodeType.Action.ToString()).ToList(); set { } }
        public List<MethodDetailsInfo> FlipflopNodes { get => MethodInfo.Where(x => x.NodeType == NodeType.Flipflop.ToString()).ToList(); set { } }
        public List<MethodDetailsInfo> UINodes { get => MethodInfo.Where(x => x.NodeType == NodeType.UI.ToString()).ToList(); set { } }

    }
}
