using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Serein.Library;
using Serein.NodeFlow.Model;
using Serein.Workbench.Avalonia.Api;
using Serein.Workbench.Avalonia.Custom.Node.ViewModels;
using Serein.Workbench.Avalonia.Custom.Views;

namespace Serein.Workbench.Avalonia.Custom.Node.Views;

public partial class ActionNodeView : NodeControlBase, INodeJunction
{
    private ActionNodeViewModel _vm;


    public ActionNodeView()
    {
        InitializeComponent();
        //_vm = App.GetService<ActionNodeViewModel>();
        //DataContext = _vm;
    }

    public NodeJunctionView ExecuteJunction => this.ExecuteJunctionControl;

    public NodeJunctionView NextStepJunction => this.NextStepJunctionControl;

    public NodeJunctionView[] ArgDataJunction => throw new System.NotImplementedException();

    public NodeJunctionView ReturnDataJunction => throw new System.NotImplementedException();
}