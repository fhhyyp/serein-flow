using Serein.Workbench.Models;
using Serein.Workbench.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        /// <summary>
        /// 流程编辑视图构造函数
        /// </summary>
        public FlowEditView()
        {
            this.DataContext = App.GetService<Locator>().FlowEditViewModel;
            InitializeComponent();
            
        }

        private void TextBlock_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            var tab = textBlock?.DataContext as FlowCanvasViewModel;
            if (tab != null)
            {
                DragDrop.DoDragDrop(textBlock, tab, DragDropEffects.Move);
            }
        }
        private void TabControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(FlowCanvasViewModel)))
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
            var sourceTab = e.Data.GetData(typeof(FlowEditorTabModel)) as FlowEditorTabModel;
            var targetTab = (sender as TabControl)?.SelectedItem as FlowEditorTabModel;
            var viewModel = (FlowEditViewModel)this.DataContext;
            if (sourceTab != null && targetTab != null && sourceTab != targetTab)
            {
                var sourceIndex = viewModel.CanvasTabs.IndexOf(sourceTab);
                var targetIndex = viewModel.CanvasTabs.IndexOf(targetTab);

                // 删除源项并插入到目标位置
                viewModel.CanvasTabs.Remove(sourceTab);
                viewModel.CanvasTabs.Insert(targetIndex, sourceTab);
                
                // 更新视图模型中的选中的Tab
                viewModel.SelectedTab = sourceTab;
            }
        }

        /// <summary>
        /// 按下确认或回车
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                if (sender is TextBox textBox
                     && textBox.DataContext is FlowEditorTabModel tab
                     && DataContext is FlowEditViewModel viewModel)
                {
                    viewModel.EndEditingTab(tab, textBox.Text); // 确认新名称
                    return;
                }
            }
        }

  
        /// <summary>
        /// 双击tab进入编辑状态
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (sender is TextBlock textBlock
                     && textBlock.DataContext is FlowEditorTabModel tab
                     && DataContext is FlowEditViewModel viewModel)
                {
                    viewModel.StartEditingTab(tab); // 确认新名称
                    return;
                }

               
            }
            
        }

        private FlowEditorTabModel lastTab;

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TabControl tabControl
                && tabControl.SelectedIndex > -1
                && DataContext is FlowEditViewModel viewModel
                && viewModel.CanvasTabs[tabControl.SelectedIndex] is FlowEditorTabModel tab)
            {
                viewModel.EndEditingTab(lastTab); // 取消编辑
                lastTab = tab;
                return;
            }

        }



    }
}
