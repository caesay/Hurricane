﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Hurricane.Extensions.Converter
{
    class NeverIfDateIsNull : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || ((DateTime)value).Year == 1)
            {
                return Application.Current.FindResource("Never").ToString();
            }
            return ((DateTime)value).ToString(Application.Current.FindResource("DateFormat").ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
