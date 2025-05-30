using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.Workbench.Services;
using Serein.Workbench.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.ViewModels
{
    internal partial class ViewCanvasInfoViewModel : ObservableObject
    {
        private readonly FlowNodeService flowNodeService;

        /// <summary>
        /// 画布数据实体
        /// </summary>
        [ObservableProperty]
        private FlowCanvasDetails _model;

        public ViewCanvasInfoViewModel(FlowNodeService flowNodeService)
        {
            this.flowNodeService = flowNodeService;
            this.flowNodeService.OnViewCanvasChanged += OnViewCanvasChanged;
        }

        /// <summary>
        /// 查看的画布发生改变
        /// </summary>
        /// <param name="flowCanvas"></param>
        private void OnViewCanvasChanged(FlowCanvasView flowCanvas)
        {
            Model = flowCanvas.ViewModel.Model;
        }



    }
}
