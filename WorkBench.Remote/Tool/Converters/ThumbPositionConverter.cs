﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Serein.WorkBench.Tool.Converters
{
    /// <summary>
    /// 画布拉动范围距离计算器
    /// </summary>
    public class RightThumbPositionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double width)
                return width - 10; // Adjust for Thumb width
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// 画布拉动范围距离计算器
    /// </summary>
    public class BottomThumbPositionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double height)
                return height - 10; // Adjust for Thumb height
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// 画布拉动范围距离计算器
    /// </summary>
    public class VerticalCenterThumbPositionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double height)
                return height / 2 - 5; // Centering Thumb vertically
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// 画布拉动范围距离计算器
    /// </summary>
    public class HorizontalCenterThumbPositionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double width)
                return width / 2 - 5; // Centering Thumb horizontally
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
