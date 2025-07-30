using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serein.Workbench.Api;
using Serein.Workbench.Models;
using Serein.Workbench.Node.View;
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
    internal partial class FlowEditViewModel : ObservableObject
    {
        /// <summary>
        /// 画布集合
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<FlowEditorTabModel> _canvasTabs = [];

        /// <summary>
        /// 当前选择的画布
        /// </summary>
        [ObservableProperty]
        private FlowEditorTabModel _selectedTab;

        private readonly FlowNodeService flowNodeService;

        public FlowEditViewModel(FlowNodeService flowNodeService)
        {
            this.flowNodeService = flowNodeService;
            

            flowNodeService.OnCreateFlowCanvasView += OnCreateFlowCanvasView; // 创建了画布
            flowNodeService.OnRemoveFlowCanvasView += OnRemoveFlowCanvasView; // 移除了画布
            //this.PropertyChanged += OnPropertyChanged;

        }

        partial void OnSelectedTabChanged(FlowEditorTabModel value)
        {
            flowNodeService.CurrentSelectCanvas = value?.Content;
            if(value is not null)
            {
                App.UIContextOperation.Invoke(() => {

                    value.Content.RefreshAllLine();
                });
            }
            
        }


        #region 响应环境事件
        private void OnCreateFlowCanvasView(FlowCanvasView canvas)
        {
            var tab = new FlowEditorTabModel(canvas);
            if(canvas is IFlowCanvas flowCanvas)
            {
                tab.Model = flowCanvas.Model;
            }
            CanvasTabs.Add(tab);
            SelectedTab = tab;
        }
        private void OnRemoveFlowCanvasView(FlowCanvasView canvas)
        {
            var tab = CanvasTabs.FirstOrDefault(c => c.Content.Guid.Equals(canvas.Guid));
            if (tab is null)
            {
                return;
            }
            CanvasTabs.Remove(tab);

            if (CanvasTabs.Count > 0 && CanvasTabs[^1] is FlowEditorTabModel c)
            {
                SelectedTab = c;
            }
        }
        #endregion




        /// <summary>
        /// 进入编辑模式
        /// </summary>
        /// <param name="tab"></param>
        public void StartEditingTab(FlowEditorTabModel tab)
        {
            if (tab != null)
            {
                tab.IsEditing = true;
                OnPropertyChanged(nameof(CanvasTabs)); // 刷新Tabs集合，以便更新UI
            }
        }

        /// <summary>
        /// 结束编辑，重命名
        /// </summary>
        /// <param name="tab"></param>
        /// <param name="newName"></param>
        public void EndEditingTab(FlowEditorTabModel tab, string? newName = null)
        {
            if (tab != null)
            {
                tab.IsEditing = false;
                if(tab.Model.Name != newName && !string.IsNullOrWhiteSpace(newName)) tab.Model.Name = newName; // 名称合法时设置新名称
                OnPropertyChanged(nameof(CanvasTabs)); // 刷新Tabs集合
            }
        }
        
    

      

    }
}
