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
    public class NotificationsViewModel : Screen, INotificationsViewModel, IHandleWithTask<IFetchNotificationsMessage>
    {
        #region fields

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(NotificationsViewModel));
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private BindableCollection<INotificationViewModel> _notifications = new BindableCollection<INotificationViewModel>();
        private string _errorMessage;
        private bool _hasNewNotifications = false;

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

        public bool HasNewNotifications
        {
            get { return this._hasNewNotifications; }
            set { this._hasNewNotifications = value; NotifyOfPropertyChange(() => this.HasNewNotifications); }
        }

        #endregion

        #region action_methods

        public async Task FetchNotificationsAsync(int page = 0)
        {
            // TODO: Don't clear the notifications list.
            this.Notifications.Clear();

            string url = (page == 0) ? "http://localhost:3000/api/v1/notifications"
                                     : "http://localhost:3000/api/v1/notifications/?page=" + page;

            var notifications = await this._deepfreezeClient.GetNotificationsAsync(url).ConfigureAwait(false);

            foreach(var notification in notifications)
            {
                var notificationVM = IoC.Get<INotificationViewModel>();
                notificationVM.Notification = notification;

                this.SetUnreadStatusInNotification(notificationVM);

                this.Notifications.Add(notificationVM);
            }

            this.UpdateHasNewNotifications();
        }

        public void SetAllNotificationsAsRead()
        {
            foreach (var notification in this.Notifications)
            {
                notification.IsNew = false;
            }

            this.UpdateHasNewNotifications();

            var latestNotificationDate = this.Notifications.Max(x => x.Notification.CreationDate);
            this.SetLastNotificationDate(latestNotificationDate);
        }

        #endregion

        #region private_methods

        private void SetUnreadStatusInNotification(INotificationViewModel notification)
        {
            DateTime mostRecentDate;
            
            if (Properties.Settings.Default.LastNotificationDate == null)
            {
                mostRecentDate = DateTime.Now.ToUniversalTime();
            }
            else
            {
                mostRecentDate = Properties.Settings.Default.LastNotificationDate.ToUniversalTime();
            }

            if (mostRecentDate >= notification.Notification.CreationDate)
            {
                notification.IsNew = false;
            }
            else
            {
                notification.IsNew = true;
            }
        }

        private void SetLastNotificationDate(DateTime date)
        {
            if (date != null)
            {
                Properties.Settings.Default.LastNotificationDate = date;
                Properties.Settings.Default.Save();
            }
        }

        private void UpdateHasNewNotifications()
        {
            if (this.Notifications.Where(x => x.IsNew).Count() > 0)
            {
                this.HasNewNotifications = true;
            }
            else
            {
                this.HasNewNotifications = false;
            }
        }

        #endregion

        #region events

        public async Task Handle(IFetchNotificationsMessage message)
        {
            if (message != null)
            {
                if (message.PagedResult != null)
                {
                    await this.FetchNotificationsAsync((int)message.PagedResult).ConfigureAwait(false);
                }
                else
                {
                    await this.FetchNotificationsAsync().ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region events

        protected async override void OnActivate()
        {
            base.OnActivate();
            this._eventAggregator.Subscribe(this);

            await this.FetchNotificationsAsync(1).ConfigureAwait(false);
        }

        protected override void OnDeactivate(bool close)
        {
            this.Notifications.Clear();

            base.OnDeactivate(close);
        }

        #endregion
    }
}
