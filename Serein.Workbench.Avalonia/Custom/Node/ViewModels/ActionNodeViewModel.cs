using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.NodeFlow.Model;
using Serein.Workbench.Avalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Custom.Node.ViewModels
{
    internal partial class ActionNodeViewModel : NodeViewModelBase
    {
        [ObservableProperty]
        private SingleActionNode? nodeMoel;

        internal override NodeModelBase NodeModelBase 
            { get => NodeMoel ?? throw new NotImplementedException(); set => NodeMoel = (SingleActionNode)value; }

        public ActionNodeViewModel()
        {
        }

    }
}
