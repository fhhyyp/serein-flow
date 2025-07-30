using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Workbench.ViewModels;
using Serein.Workbench.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.Workbench.Models
{
    /// <summary>
    /// FlowEditorTabModel 类表示一个流程编辑器的标签模型。
    /// </summary>
    public partial class FlowEditorTabModel : ObservableObject
    {
        [ObservableProperty]
        private FlowCanvasDetails? _model;


        /// <summary>
        /// 正在选中
        /// </summary>
        [ObservableProperty]
        private bool _isSelected;

        /// <summary>
        /// 正在编辑标题
        /// </summary>
        [ObservableProperty]
        private bool _isEditing;

        /// <summary>
        /// tab对应的控件
        /// </summary>
        [ObservableProperty]
        private FlowCanvasView? content;

        /// <summary>
        /// FlowEditorTabModel 构造函数
        /// </summary>
        /// <param name="content"></param>

        public FlowEditorTabModel(FlowCanvasView content)
        {
            this.Content = content;
        }

    }


}
