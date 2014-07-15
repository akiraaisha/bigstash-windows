using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

using DeepfreezeModel;

namespace DeepfreezeApp
{
    public class StatusToDeleteButtonVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var status = (Enumerations.Status)value;

            Visibility buttonVisibility = Visibility.Collapsed;

            switch(status)
            {
                case Enumerations.Status.Uploading:
                case Enumerations.Status.Paused:
                case Enumerations.Status.Failed:
                case Enumerations.Status.UnableToStart:
                    buttonVisibility = Visibility.Visible;
                    break;
                
                case Enumerations.Status.Uploaded:
                case Enumerations.Status.Completed:
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
