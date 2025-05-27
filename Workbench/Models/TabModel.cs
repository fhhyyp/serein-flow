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
    public partial class FlowEditorTabModel : ObservableObject
    {
        /// <summary>
        /// tab 名称
        /// </summary>
       /* public string Name
        {
            get
            {

                var vm = (FlowCanvasViewModel)Content.DataContext;
                return vm.Model.Name ?? "null";
            }
            set
            {
                var vm = (FlowCanvasViewModel)Content.DataContext;
                vm.Model.Name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
*/

        [ObservableProperty]
        private FlowCanvasDetails _model;


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
        private FlowCanvasView content;


        public FlowEditorTabModel(FlowCanvasView content)
        {
            this.Content = content;
        }

    }


}
