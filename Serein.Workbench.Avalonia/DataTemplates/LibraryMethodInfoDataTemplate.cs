using Avalonia.Controls.Templates;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serein.Library;
using System.Diagnostics;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia;
using Serein.Library.Utils;

namespace Serein.Workbench.Avalonia.DataTemplates
{

    /// <summary>
    /// 方法信息模板
    /// </summary>
    internal class LibraryMethodInfoDataTemplate : IDataTemplate
    {
        public Control Build(object param)
        {
            if (param is MethodDetailsInfo mdInfo)
            {
                var textBlock = new TextBlock() { Text = mdInfo.MethodAnotherName };
                textBlock.Margin = new Thickness(2d, -6d, 2d, -6d);
                textBlock.FontSize = 12;
                textBlock.PointerPressed += TextBlock_PointerPressed;
                textBlock.Tag = mdInfo;
                return textBlock;
            }
            else
            {
                var textBlock = new TextBlock() { Text = $"Binding 类型不为预期的[MethodDetailsInfo]，而是[{param?.GetType()}]" };
                textBlock.Margin = new Thickness(2d, -6d, 2d, -6d);
                textBlock.FontSize = 12;
                return textBlock;
            }

        }

        public bool Match(object data)
        {
            return data is MethodDetailsInfo;
        }


        private static void TextBlock_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not TextBlock textBlock || textBlock.Tag is not MethodDetailsInfo mdInfo)
            {
                return;
            }
            var dragData = new DataObject(); // 设置需要传递的数据
            dragData.Set(DataFormats.Text, mdInfo.ToJsonText());
            _ = DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy);
            //var result = await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy);
            //Debug.WriteLine("DoDrag :" + result);
            //switch (result)
            //{
            //    case DragDropEffects.Copy:
            //        Debug.WriteLine("文本来自 Copy");
            //        break;
            //    case DragDropEffects.Link:
            //        Debug.WriteLine("文本来自 Link");
            //        break;
            //    case DragDropEffects.None:
            //        Debug.WriteLine("拖拽操作被取消");
            //        break;
            //}
        }



    }

}
