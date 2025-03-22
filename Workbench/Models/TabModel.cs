using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;
using Serein.Workbench.ViewModels;
using Serein.Workbench.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.Workbench.Models
{
    public partial class FlowCanvasModel : ObservableObject
    {
        public string Name
        {
            get
            {

                var vm = (FlowCanvasViewModel)content.DataContext;
                return vm.Name;
            }
            set
            {
                var vm = (FlowCanvasViewModel)content.DataContext;
                vm.Name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        [ObservableProperty]
        private bool _isSelected;
        [ObservableProperty]
        private bool _isEditing;
        [ObservableProperty]
        private FlowCanvasView content;


        public FlowCanvasModel()
        {
            
        }

    }


}
