using Avalonia.Controls;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.Workbench.Avalonia.Custom.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Serein.Workbench.Avalonia.ViewModels;

namespace Serein.Workbench.Avalonia.Custom.ViewModels
{


    internal partial class ParameterDetailsViewModel : ViewModelBase
    {

        public ParameterDetails ParameterDetails {  get; set; } 

        public ParameterDetailsViewModel() 
        {
        }
        public ParameterDetailsViewModel(ParameterDetails parameterDetails) 
        { 
            ParameterDetails = parameterDetails;
            RefreshIsVisible();

            // 监视“是否为显式参数”更改
            ParameterDetails.PropertyChanged += (o, e) =>
            {
                if (nameof(ParameterDetails.IsExplicitData).Equals(e.PropertyName)) 
                    RefreshIsVisible();
            };
        }

       
        private void RefreshIsVisible()
        {
            if (!ParameterDetails.IsExplicitData)
            {
                // 并非显式设置参数
                IsVisibleA = true;
                IsVisibleB = false;
                IsVisibleC = false;
                return;
            }

            if ("Value".Equals(ParameterDetails.ExplicitTypeName))
            {
                // 值类型
                IsVisibleA = false;
                IsVisibleB = true;
                IsVisibleC = false;
            }
            else
            {
                // 选项类型
                IsVisibleA = false;
                IsVisibleB = false;
                IsVisibleC = true;
            }
        }

        [ObservableProperty]
        private bool isVisibleA;
        [ObservableProperty]
        private bool isVisibleB;
        [ObservableProperty]
        private bool isVisibleC;
    }
}
