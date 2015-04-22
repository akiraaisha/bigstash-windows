using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

using BigStash.Model;

namespace BigStash.WPF
{
    public class StatusToDeleteButtonVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var status = (Enumerations.Status)value;

            Visibility buttonVisibility = Visibility.Collapsed;

            switch(status)
            {
                
                case Enumerations.Status.Paused:
                case Enumerations.Status.Error:
                    buttonVisibility = Visibility.Visible;
                    break;
                case Enumerations.Status.Pending:
                case Enumerations.Status.Uploading:
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
