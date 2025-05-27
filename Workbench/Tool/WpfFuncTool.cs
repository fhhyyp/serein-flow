using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

namespace Serein.Workbench.Tool
{
    internal static class WpfFuncTool
    { /// <summary>
      /// 创建菜单子项
      /// </summary>
      /// <param name="header"></param>
      /// <param name="handler"></param>
      /// <returns></returns>
        public static MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
        {
            var menuItem = new MenuItem { Header = header };
            menuItem.Click += handler;
            return menuItem;
        }



        /// <summary>
        /// 穿透元素获取区域容器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="element"></param>
        /// <returns></returns>
        public static T? GetParentOfType<T>(DependencyObject element) where T : DependencyObject
        {
            while (element != null)
            {
                if (element is T e)
                {
                    return e;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

    }
}
