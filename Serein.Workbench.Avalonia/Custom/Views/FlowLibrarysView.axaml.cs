using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Serein.Library;
using Serein.Workbench.Avalonia.Custom.ViewModels;
using System;

namespace Serein.Workbench.Avalonia.Custom.Views;

public partial class FlowLibrarysView : UserControl
{
    public FlowLibrarysView()
    {
        InitializeComponent();
        DataContext = App.GetService<FlowLibrarysViewModel>();
    }
}