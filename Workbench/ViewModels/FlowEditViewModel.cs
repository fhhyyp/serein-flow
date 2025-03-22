using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serein.Workbench.Models;
using Serein.Workbench.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Serein.Workbench.ViewModels
{
    /// <summary>
    /// 流程编辑数据视图
    /// </summary>
    public partial class FlowEditViewModel : ObservableObject
    {
        public ObservableCollection<FlowCanvasModel> Tabs { get; set; }
        public ICommand AddTabCommand { get; set; }
        public ICommand RemoveTabCommand { get; set; }
        public ICommand RenameTabCommand { get; set; }

        [ObservableProperty]
        private FlowCanvasModel _selectedTab;

        private int _addCount = 0;

        public FlowEditViewModel()
        {
            Tabs = new ObservableCollection<FlowCanvasModel>();
            AddTabCommand = new RelayCommand(AddTab);
            RemoveTabCommand = new RelayCommand(RemoveTab, CanRemoveTab);

            // 初始化时添加一个默认的Tab
            AddTab(); // 添加一个默认选项卡
        }

        private void AddTab()
        {
            var flowCanvasView = new FlowCanvasView(); // 创建FlowCanvasView实例
            Tabs.Add(new FlowCanvasModel { Content = flowCanvasView ,Name = $"New Tab {_addCount++}"});
            SelectedTab = Tabs[Tabs.Count - 1]; // 选择刚添加的Tab
        }

        private void RemoveTab()
        {
            if (Tabs.Count > 0 && SelectedTab != null)
            {
                Tabs.Remove(SelectedTab);
                SelectedTab = Tabs.Count > 0 ? Tabs[Tabs.Count - 1] : null;
            }
        }

        private bool CanRemoveTab()
        {
            return SelectedTab != null;
        }

       

        /// <summary>
        /// 进入编辑模式
        /// </summary>
        /// <param name="tab"></param>
        public void StartEditingTab(FlowCanvasModel tab)
        {
            if (tab != null)
            {
                tab.IsEditing = true;
                OnPropertyChanged(nameof(Tabs)); // 刷新Tabs集合，以便更新UI
            }
        }

        /// <summary>
        /// 结束编辑，重命名
        /// </summary>
        /// <param name="tab"></param>
        /// <param name="newName"></param>
        public void EndEditingTab(FlowCanvasModel tab, string newName)
        {
            if (tab != null)
            {
                tab.IsEditing = false;
                tab.Name = newName; // 设置新名称
                OnPropertyChanged(nameof(Tabs)); // 刷新Tabs集合
            }
        }
        
    

      

    }
}
