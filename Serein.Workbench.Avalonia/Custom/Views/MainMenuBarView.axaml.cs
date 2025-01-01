using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Serein.Workbench.Avalonia.Custom.ViewModels;
using System;

namespace Serein.Workbench.Avalonia.Custom.Views;

public partial class MainMenuBarView : UserControl
{
    public MainMenuBarView()
    {
        InitializeComponent();
        DataContext =  App.GetService<MainMenuBarViewModel>();
    }
}