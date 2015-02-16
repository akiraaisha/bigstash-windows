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
        private Notification _notification;

        public  string CreationDateText
        {
            get
            {
                return this._notification.CreationDate.ToString();
            }
        }
        public Notification Notification
        {
            get { return this._notification; }
            set 
            {
                this._notification = value; 
                NotifyOfPropertyChange(() => this.Notification); 
            }
        }
    }
}
