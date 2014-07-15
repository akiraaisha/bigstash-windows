using DeepfreezeModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DeepfreezeApp
{
    public class UploadStatusToText : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var status = (Enumerations.Status)value;

            string statusString = String.Empty;

            switch(status)
            {
                case Enumerations.Status.Pending:
                    statusString = Properties.Resources.StatusPendingText;
                    break;
                case Enumerations.Status.Uploaded:
                    statusString = Properties.Resources.StatusUploadedText;
                    break;
                case Enumerations.Status.Uploading:
                    statusString = Properties.Resources.StatusUploadingText;
                    break;
                case Enumerations.Status.Paused:
                    statusString = Properties.Resources.StatusPausedText;
                    break;
                case Enumerations.Status.Completed:
                    statusString = Properties.Resources.StatusCompletedText;
                    break;
                case Enumerations.Status.Creating:
                    statusString = Properties.Resources.StatusCreatingText;
                    break;
                case Enumerations.Status.Failed:
                    statusString = Properties.Resources.StatusFailedText;
                    break;
                case Enumerations.Status.UnableToStart:
                    statusString = Properties.Resources.StatusUnableToStartText;
                    break;
            }

            return statusString;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
