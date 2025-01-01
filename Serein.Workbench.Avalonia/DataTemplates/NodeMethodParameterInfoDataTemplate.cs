using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serein.Library;
using Serein.Workbench.Avalonia.Custom.Views;
using Serein.Workbench.Avalonia.Custom.ViewModels;

namespace Serein.Workbench.Avalonia.DataTemplates
{
    internal class NodeMethodParameterInfoDataTemplate : IDataTemplate
    {
        public Control Build(object param)
        {
            if (param is ParameterDetails mdInfo)
            {
                var viewModel = new ParameterDetailsViewModel(mdInfo);
                var view = new ParameterDetailsInfoView(viewModel);
                return view;
            }
            else
            {
                var textBlock = new TextBlock() { Text = $"Binding 类型不为预期的[MethodDetailsInfo]，而是[{param?.GetType()}]" };
                textBlock.Margin = new Thickness(1d, -4d, 1d, -4d);
                return textBlock;
            }

        }

        public bool Match(object data)
        {
            return data is ParameterDetails;
        }
    }
}
