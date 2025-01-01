using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Serein.Library;
using Serein.Workbench.Avalonia.Api;
using Serein.Workbench.Avalonia.Custom.Node.ViewModels;

namespace Serein.Workbench.Avalonia.Custom.Node.Views;

public partial class ActionNodeView : UserControl, INodeControl
{
    private ActionNodeViewModel _vm;
    public ActionNodeView()
    {
        InitializeComponent();
        _vm = App.GetService<ActionNodeViewModel>();
        DataContext = _vm;
    }

    NodeModelBase INodeControl.NodeModelBase => _vm.NodeModelBase ?? throw new System.NotImplementedException();  // �����ڵ�

    void INodeControl.SetNodeModel(NodeModelBase nodeModel) // �����ڵ�
    {
        _vm.NodeModelBase = nodeModel;
    }
}