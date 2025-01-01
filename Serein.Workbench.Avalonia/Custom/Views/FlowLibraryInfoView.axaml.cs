using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Serein.Library;
using Serein.Workbench.Avalonia.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Serein.Workbench.Avalonia.Custom.Views;

public partial class FlowLibraryInfoView : TemplatedControl
{

    private IWorkbenchEventService workbenchEventService;

    public FlowLibraryInfoView()
    {
        workbenchEventService = App.GetService<IWorkbenchEventService>();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // 如果改变的属性是 MdsProperty ，则加载方法信息
        if (change.Property == MdsProperty)
        {
            if(change.NewValue is MethodDetailsInfo[] value)
            {
                onNext(value);
            }
        }
    }

    /// <summary>
    /// 获取到控件信息
    /// </summary>
    /// <param name="e"></param>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        #region 动作节点方法信息
        if (e.NameScope.Find("PART_ActionMethodInfos") is ListBox p_am)
        {
            p_am.AddHandler(InputElement.PointerExitedEvent, ListBox_PointerExited);
            p_am.AddHandler(SelectingItemsControl.SelectionChangedEvent, ListBox_SelectionChanged);
        }

        #endregion
        #region 触发器节点方法信息
        if (e.NameScope.Find("PART_FlipflopMethodInfos") is ListBox p_fm)
        {
            p_fm.SelectionChanged += ListBox_SelectionChanged;
            p_fm.PointerExited += ListBox_PointerExited;
        }
        #endregion
    }
   

    private void ListBox_SelectionChanged(object? o, SelectionChangedEventArgs e)
    {
        if (o is ListBox listBox && listBox.SelectedIndex > 0 && listBox.SelectedItem is MethodDetailsInfo mdInfo)
        {
            workbenchEventService.PreviewLibraryMethodInfo(mdInfo); // 通知其它地方预览了某个方法信息
        }
    }
    private void ListBox_PointerExited(object? o, PointerEventArgs e)
    {
        if (o is ListBox listBox && listBox.SelectedIndex > -1)
        {
            listBox.SelectedIndex = -1; // 如果鼠标离开了，取消已选状态
        }
    }



    /// <summary>
    /// 将信息加载出来
    /// </summary>
    /// <param name="value"></param>
    private void onNext(MethodDetailsInfo[] value)
    {
        if(value is null)
        {
            return;
        }
        var fmd = value.Where(item => nameof(NodeType.Flipflop).Equals(item.NodeType));
        FlipflopMethods = new ObservableCollection<MethodDetailsInfo>(fmd);
        var amd = value.Where(item => nameof(NodeType.Action).Equals(item.NodeType));
        ActionMethods = new ObservableCollection<MethodDetailsInfo>(amd);
    }


    #region Template Public Property / 控件公开属性
    public static readonly DirectProperty<FlowLibraryInfoView, string> LibraryNameProperty =
        AvaloniaProperty.RegisterDirect<FlowLibraryInfoView, string>(nameof(LibraryName), o => o.LibraryName, (o, v) => o.LibraryName = v);
    public static readonly DirectProperty<FlowLibraryInfoView, MethodDetailsInfo[]> MdsProperty =
        AvaloniaProperty.RegisterDirect<FlowLibraryInfoView, MethodDetailsInfo[]>(nameof(Mds), o => o.Mds, (o, v) => o.Mds = v);
    public static readonly DirectProperty<FlowLibraryInfoView, ObservableCollection<MethodDetailsInfo>> ActionMethodsProperty =
        AvaloniaProperty.RegisterDirect<FlowLibraryInfoView, ObservableCollection<MethodDetailsInfo>>(nameof(ActionMethods), o => o.ActionMethods, (o, v) => o.ActionMethods = v);
    public static readonly DirectProperty<FlowLibraryInfoView, ObservableCollection<MethodDetailsInfo>> FlipflopMethodsProperty =
       AvaloniaProperty.RegisterDirect<FlowLibraryInfoView, ObservableCollection<MethodDetailsInfo>>(nameof(FlipflopMethods), o => o.FlipflopMethods, (o, v) => o.FlipflopMethods = v);

    private string libraryName = string.Empty;
    private ObservableCollection<MethodDetailsInfo> actionMethods;
    private ObservableCollection<MethodDetailsInfo> flipflopMethods;
    private MethodDetailsInfo[] mds = [];


    public string LibraryName
    {
        get { return libraryName; }
        set { SetAndRaise(LibraryNameProperty, ref libraryName, value); }
    }

/*
    public static readonly AttachedProperty<string> LibraryName2Property = AvaloniaProperty.RegisterAttached<FlowLibraryInfoView, Control, string>("LibraryName2");

    public static string GetLibraryName2(Control element)
    {
        return element.GetValue(LibraryName2Property);
    }

    public static void SetLibraryName2(Control element, string value)
    {
       element.SetValue(LibraryName2Property, value);
    }
 */

    /// <summary>
    /// Method Info
    /// 方法信息
    /// </summary>
    public MethodDetailsInfo[] Mds
    {
        get { return mds; }
        set
        {
            SetAndRaise(MdsProperty, ref mds, value);
        }
    }
    /// <summary>
    /// 动作节点的方法
    /// </summary>
    public ObservableCollection<MethodDetailsInfo> ActionMethods
    {
        get { return actionMethods; }
        set
        {
            SetAndRaise(ActionMethodsProperty, ref actionMethods, value);
        }
    }

    /// <summary>
    /// 触发器节点的方法
    /// </summary>
    public ObservableCollection<MethodDetailsInfo> FlipflopMethods
    {
        get { return flipflopMethods; }
        set
        {

            SetAndRaise(FlipflopMethodsProperty, ref flipflopMethods, value);
        }
    }

    #endregion
}


public class ItemsChangeObservableCollection<T> : ObservableCollection<T> where T : INotifyPropertyChanged
{
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            RegisterPropertyChanged(e.NewItems);
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            UnRegisterPropertyChanged(e.OldItems);
        }
        else if (e.Action == NotifyCollectionChangedAction.Replace)
        {
            UnRegisterPropertyChanged(e.OldItems);
            RegisterPropertyChanged(e.NewItems);
        }


        base.OnCollectionChanged(e);
    }

    protected override void ClearItems()
    {
        UnRegisterPropertyChanged(this);
        base.ClearItems();
    }
    private void RegisterPropertyChanged(IList items)
    {
        foreach (INotifyPropertyChanged item in items)
        {
            if (item != null)
            {
                item.PropertyChanged += new PropertyChangedEventHandler(item_PropertyChanged);
            }
        }
    }
    private void UnRegisterPropertyChanged(IList items)
    {
        foreach (INotifyPropertyChanged item in items)
        {
            if (item != null)
            {
                item.PropertyChanged -= new PropertyChangedEventHandler(item_PropertyChanged);
            }
        }
    }
    private void item_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        //launch an event Reset with name of property changed
        base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
