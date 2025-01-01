using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Serein.Library;
using Serein.Workbench.Avalonia.Custom.ViewModels;
using Serein.Workbench.Avalonia.Custom.Views;
using Serein.Workbench.Avalonia.Services;
using System.Diagnostics;

namespace Serein.Workbench.Avalonia.Custom.Views;

public partial class FlowLibraryMethodInfoView : UserControl
{
    private FlowLibraryMethodInfoViewModel _vm;
    public FlowLibraryMethodInfoView()
    {
        InitializeComponent();
        _vm = App.GetService<FlowLibraryMethodInfoViewModel>();
        DataContext = _vm;
        //this.PointerPressed += FlowLibraryMethodInfoView_PointerPressed;
    }

    //private async void FlowLibraryMethodInfoView_PointerPressed(object? sender, PointerPressedEventArgs e)
    //{
    //    if (_vm.MethodDetailsInfo is null)
    //    {
    //        return;
    //    }
    //    DataObject dragData = new DataObject();
    //    dragData.Set(DataFormats.Text, $"{_vm.MethodDetailsInfo.MethodAnotherName}");
    //    var result = await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy);
    //    Debug.WriteLine("DoDrag :" + result);
    //    switch (result)
    //    {
    //        case DragDropEffects.Copy:
    //            Debug.WriteLine("文本来自 Copy");
    //            break;
    //        case DragDropEffects.Link:
    //            Debug.WriteLine("文本来自 Link");
    //            break;
    //        case DragDropEffects.None:
    //            Debug.WriteLine("拖拽操作被取消");
    //            break;
    //    }
    //}



}