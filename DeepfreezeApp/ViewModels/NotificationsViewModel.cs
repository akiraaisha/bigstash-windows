using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using Caliburn.Micro;
using DeepfreezeModel;
using DeepfreezeSDK;

namespace DeepfreezeApp
{
    [Export(typeof(INotificationsViewModel))]
    public class NotificationsViewModel : Screen, INotificationsViewModel
    {
        #region fields

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(NotificationsViewModel));
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private BindableCollection<INotificationViewModel> _notifications = new BindableCollection<INotificationViewModel>();
        private string _errorMessage;

        #endregion

        #region constructor

        [ImportingConstructor]
        public NotificationsViewModel(IDeepfreezeClient deepfreezeClient, IEventAggregator eventAggregator)
        {
            this._deepfreezeClient = deepfreezeClient;
            this._eventAggregator = eventAggregator;
        }

        #endregion

        #region properties

        public BindableCollection<INotificationViewModel> Notifications
        {
            get { return this._notifications; }
            set { this._notifications = value; NotifyOfPropertyChange(() => this.Notifications); }
        }

        public string ErrorMessage
        {
            get { return this._errorMessage; }
            set { this._errorMessage = value; NotifyOfPropertyChange(() => this.ErrorMessage); }
        }

        #endregion

        #region action_methods

        public async Task FetchNotifications()
        {
            string url = "http://localhost:3000/api/v1/notifications";

            var notifications = await this._deepfreezeClient.GetNotificationsAsync(url);

            foreach(var notification in notifications)
            {
                var notificationVM = IoC.Get<INotificationViewModel>();
                notificationVM.Notification = notification;
                this.Notifications.Add(notificationVM);
            }
        }

        #endregion

        #region events

        protected async override void OnActivate()
        {
            base.OnActivate();

            await this.FetchNotifications().ConfigureAwait(false);
        }

        #endregion
    }
}
