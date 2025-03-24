using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serein.Workbench.Models;
using Serein.Workbench.Services;
using Serein.Workbench.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Serein.Workbench.ViewModels
{
    /// <summary>
    /// 流程编辑数据视图
    /// </summary>
    public partial class FlowEditViewModel : ObservableObject
    {
        public ObservableCollection<FlowCanvasModel> Tabs { get; set; } = [];
        public ICommand AddTabCommand { get; set; }
        public ICommand RemoveTabCommand { get; set; }
        public ICommand RenameTabCommand { get; set; }

        [ObservableProperty]
        private FlowCanvasModel _selectedTab;

        private int _addCount = 0;
        private readonly FlowNodeService flowNodeService;

        public FlowEditViewModel(FlowNodeService flowNodeService)
        {
            this.flowNodeService = flowNodeService;
            AddTabCommand = new RelayCommand(AddTab);
            RemoveTabCommand = new RelayCommand(RemoveTab, CanRemoveTab);

            flowNodeService.OnCreateFlowCanvasView += OnCreateFlowCanvasView; // 环境创建了节点
            flowNodeService.OnRemoveFlowCanvasView += OnRemoveFlowCanvasView;
            this.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object? value, PropertyChangedEventArgs e)
        {
            if (nameof(SelectedTab).Equals(e.PropertyName) && value is FlowCanvasModel model)
            {
                flowNodeService.CurrentSelectCanvas = model.Content;  // 选中的视图发生改变
            }
        }

        #region 响应环境事件
        private void OnCreateFlowCanvasView(FlowCanvasView FlowCanvasView)
        {
            var model = new FlowCanvasModel { Content = FlowCanvasView, Name = FlowCanvasView.ViewModel.Name };
            Tabs.Add(model);
        }
        private void OnRemoveFlowCanvasView(string canvasGuid)
        {
            var tab = Tabs.FirstOrDefault(t => t.Content.ViewModel.Model.Guid.Equals(canvasGuid, StringComparison.OrdinalIgnoreCase));
            if (tab is null)
            {
                return;
            }
            Tabs.Remove(tab);
            Tabs.Remove(SelectedTab);
            if(Tabs.Count > 0 && Tabs[^1] is FlowCanvasModel view )
            {
                SelectedTab = view;
            }
        }
        #endregion


        private void AddTab() => flowNodeService.CreateFlowCanvas();
        private void RemoveTab()
        {
            if (Tabs.Count > 0 && SelectedTab != null) flowNodeService.RemoveFlowCanvas();
        }

        private bool CanRemoveTab() => SelectedTab != null;



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
