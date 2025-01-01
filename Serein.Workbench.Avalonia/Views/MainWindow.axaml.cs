using Avalonia.Controls;
using Avalonia.Input;
using System.Diagnostics;
using System;
using Avalonia.Markup.Xaml;
using Newtonsoft.Json;
using Serein.NodeFlow.Env;
using System.Threading.Tasks;
using Serein.Library;
using Serein.Workbench.Avalonia.Services;

namespace Serein.Workbench.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        IKeyEventService keyEventService = App.GetService<IKeyEventService>();
        this.Loaded += MainWindow_Loaded;
        this.KeyDown += (o, e) =>
        {
            keyEventService.SetKeyState(e.Key, true);
        };
        this.KeyUp += (o, e) =>
        {
            keyEventService.SetKeyState(e.Key, false);
        };
    }

    private void MainWindow_Loaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        
        
    }
}
