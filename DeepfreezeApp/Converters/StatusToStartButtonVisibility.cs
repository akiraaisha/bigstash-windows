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
    public class StatusToStartButtonVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var status = (Enumerations.Status)value;

            Visibility buttonVisibility = Visibility.Collapsed;

            switch(status)
            {
                case Enumerations.Status.Paused:
                    buttonVisibility = Visibility.Visible;
                    break;
                case Enumerations.Status.Uploading:
                case Enumerations.Status.Failed:
                case Enumerations.Status.UnableToStart:
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
