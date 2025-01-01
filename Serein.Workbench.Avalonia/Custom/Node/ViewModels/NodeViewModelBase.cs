using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.Workbench.Avalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Custom.Node.ViewModels
{
    /// <summary>
    /// 节点ViewModel基类
    /// </summary>
    internal abstract class NodeViewModelBase : ViewModelBase
    {
        internal abstract NodeModelBase NodeModelBase { get; set; }
    }
}
