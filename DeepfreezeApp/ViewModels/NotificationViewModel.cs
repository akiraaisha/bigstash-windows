using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using DeepfreezeModel;

using Caliburn.Micro;

namespace DeepfreezeApp
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(INotificationViewModel))]
    public class NotificationViewModel : PropertyChangedBase, INotificationViewModel
    {
        #region fields

        private Notification _notification;
        private bool _isNew = false;

        #endregion

        #region properties

        public  string CreationDateText
        {
            get
            {
                return this._notification.CreationDate.ToString("{dd/MM/yyyy hh:mm:ss}");
            }
        }

        public Notification Notification
        {
            get { return this._notification; }
            set 
            {
                var not = value;

                // replace the ending of href tags since that's easy.
                not.Verb = not.Verb.Replace("</a>", "");

                // Strip the verb from all href tags.
                // Get the <a and </a> index.

                while(true)
                {
                    var startIndex = not.Verb.IndexOf("<a href=\"");
                    var endIndex = not.Verb.IndexOf("\">");

                    if (startIndex < endIndex)
                    {
                        not.Verb = not.Verb.Remove(startIndex, endIndex - startIndex + 2);
                    }
                    else
                    {
                        break;
                    }
                }
                

                this._notification = not; 
                NotifyOfPropertyChange(() => this.Notification); 
            }
        }

        public bool IsNew
        {
            get { return this._isNew; }
            set { this._isNew = value; NotifyOfPropertyChange(() => this.IsNew); }
        }

        #endregion
    }
}
