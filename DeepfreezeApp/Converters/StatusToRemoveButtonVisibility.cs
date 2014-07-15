﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

using DeepfreezeModel;

namespace DeepfreezeApp
{
    public class StatusToRemoveButtonVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var status = (Enumerations.Status)value;

            Visibility buttonVisibility = Visibility.Collapsed;

            switch(status)
            {
                case Enumerations.Status.Completed:
                    buttonVisibility = Visibility.Visible;
                    break;
                default:
                    buttonVisibility = Visibility.Collapsed;
                    break;
            }

            return buttonVisibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
