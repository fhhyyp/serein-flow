using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Serein.Library;
using Serein.Workbench.Avalonia.Custom.ViewModels;

namespace Serein.Workbench.Avalonia.Custom.Views;

internal partial class ParameterDetailsInfoView : UserControl
{
    private readonly ParameterDetailsViewModel _vm;
    public ParameterDetailsInfoView()
    {
        InitializeComponent();
        var pd = new ParameterDetails();
        pd.Name = "param name";
        pd.IsParams = true;
        pd.DataValue = "data value";
        pd.Items = ["A","B","C"];
        _vm = new (pd);
        DataContext = _vm;
    }
    public ParameterDetailsInfoView(ParameterDetailsViewModel parameterDetailsViewModel)
    {
        InitializeComponent();
        _vm = parameterDetailsViewModel;
        DataContext = _vm;
    }


}