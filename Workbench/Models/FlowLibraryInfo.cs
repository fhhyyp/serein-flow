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
    public partial class FlowLibraryMethodDetailsInfo(MethodDetailsInfo info): ObservableObject
    {
        [ObservableProperty]
        private string _anotherName = info.MethodAnotherName;

        [ObservableProperty]
        private string _assmblyName = info.AssemblyName;

        [ObservableProperty]
        private string _methodName = info.MethodName;

        [ObservableProperty]
        private string _nodeType = info.NodeType;
    }

    internal partial class FlowLibraryInfo : ObservableObject
    {
        [ObservableProperty]
        private string _filePath;

        [ObservableProperty]
        private string _libraryName;

        [ObservableProperty]
        private ObservableCollection<FlowLibraryMethodDetailsInfo> _methodInfo;


        public List<FlowLibraryMethodDetailsInfo> ActionNodes { get =>  MethodInfo.Where(x => x.NodeType == NodeType.Action.ToString()).ToList(); set { } }
        public List<FlowLibraryMethodDetailsInfo> FlipflopNodes { get => MethodInfo.Where(x => x.NodeType == NodeType.Flipflop.ToString()).ToList(); set { } }
        public List<FlowLibraryMethodDetailsInfo> UINodes { get => MethodInfo.Where(x => x.NodeType == NodeType.UI.ToString()).ToList(); set { } }

    }
}
