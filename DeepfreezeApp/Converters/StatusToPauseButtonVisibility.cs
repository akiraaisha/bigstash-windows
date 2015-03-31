using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

using DeepfreezeModel;

namespace BigStash.WPF
{
    public class StatusToPauseButtonVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var status = (Enumerations.Status)value;

            Visibility buttonVisibility = Visibility.Collapsed;

            switch(status)
            {
                case Enumerations.Status.Uploading:
                
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
