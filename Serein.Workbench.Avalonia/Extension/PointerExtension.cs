using Avalonia.Controls;
using Avalonia.Input;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Extension
{
    internal static class PointerExtension
    {
        public static bool JudgePointer(this
                                        PointerEventArgs eventArgs, 
                                        object? sender,
                                        PointerType pointerType,
                                        Func<PointerPointProperties,bool> judgePointerFunc)
        {
            if(sender is not Visual visual)
            {
                return false;
            }
            if (eventArgs.Pointer.Type == pointerType) // 是否是否是指定的设备类型
            {
                var point = eventArgs.GetCurrentPoint(visual);  // 获取到点击点
                return judgePointerFunc.Invoke(point.Properties); // 判断是否属于某种类型的点击
            }
            return false;
        }
    }
}
