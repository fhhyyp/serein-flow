using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Serein.Library;
using Serein.NodeFlow.Model;
using Serein.Workbench.Avalonia.Api;
using Serein.Workbench.Avalonia.Custom.Node.ViewModels;

namespace Serein.Workbench.Avalonia.Custom.Node.Views;

public partial class ActionNodeView : NodeControlBase
{
    private ActionNodeViewModel _vm;


    public ActionNodeView()
    {
        InitializeComponent();
        //_vm = App.GetService<ActionNodeViewModel>();
        //DataContext = _vm;
    }

}