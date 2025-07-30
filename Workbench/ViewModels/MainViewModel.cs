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
    /// <summary>
    /// 主视图模型
    /// </summary>
    public class MainViewModel : ObservableObject
    {
        private readonly IKeyEventService keyEventService;

        /// <summary>
        /// 主视图模型构造函数
        /// </summary>
        /// <param name="keyEventService"></param>
        public MainViewModel(IKeyEventService keyEventService)
        {
            
            this.keyEventService = keyEventService;
        }

        
    }
}
