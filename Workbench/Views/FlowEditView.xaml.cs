using Serein.Workbench.Models;
using Serein.Workbench.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using TabControl = System.Windows.Controls.TabControl;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace Serein.Workbench.Views
{
    /// <summary>
    /// FlowEditView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowEditView : UserControl
    {
        public FlowEditView()
        {
            this.DataContext = App.GetService<Locator>().FlowEditViewModel;
            InitializeComponent();
        }

        private void TextBlock_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            var tab = textBlock?.DataContext as FlowCanvasModel;
            if (tab != null)
            {
                DragDrop.DoDragDrop(textBlock, tab, DragDropEffects.Move);
            }
        }
        private void TabControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(FlowCanvasModel)))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void TabControl_Drop(object sender, DragEventArgs e)
        {
            var sourceTab = e.Data.GetData(typeof(FlowCanvasModel)) as FlowCanvasModel;
            var targetTab = (sender as TabControl)?.SelectedItem as FlowCanvasModel;
            var viewModel = (FlowEditViewModel)this.DataContext;
            if (sourceTab != null && targetTab != null && sourceTab != targetTab)
            {
                var sourceIndex = viewModel.Tabs.IndexOf(sourceTab);
                var targetIndex = viewModel.Tabs.IndexOf(targetTab);

                // 删除源项并插入到目标位置
                viewModel.Tabs.Remove(sourceTab);
                viewModel.Tabs.Insert(targetIndex, sourceTab);

                // 更新视图模型中的选中的Tab
                viewModel.SelectedTab = sourceTab;
            }
        }


        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
         
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                var textBox = sender as TextBox;
                var newName = textBox?.Text;
                if (string.IsNullOrEmpty(newName))
                {
                    return;
                }
                var tab = textBox?.DataContext as FlowCanvasModel;
                if (tab != null)
                {
                    var viewModel = (FlowEditViewModel)this.DataContext;
                    viewModel.EndEditingTab(tab, newName); // 确认新名称
                }
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var newName = textBox?.Text;
            if (string.IsNullOrEmpty(newName))
            {
                return;
            }
            var tab = textBox?.DataContext as FlowCanvasModel;
            if (tab != null && tab.IsEditing)
            {
                var viewModel = (FlowEditViewModel)this.DataContext;
                viewModel.EndEditingTab(tab, newName); // 确认新名称
            }
        }

        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                var textBlock = sender as TextBlock;
                var tab = textBlock?.DataContext as FlowCanvasModel;
                if (tab != null)
                {
                    var viewModel = (FlowEditViewModel)this.DataContext;
                    viewModel.StartEditingTab(tab);
                }
            }
            
        }
    }
}
