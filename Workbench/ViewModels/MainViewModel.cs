using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Workbench.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace Serein.Workbench.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly IKeyEventService keyEventService;

        public MainViewModel(IKeyEventService keyEventService)
        {
            
            this.keyEventService = keyEventService;
        }

        
    }
}
