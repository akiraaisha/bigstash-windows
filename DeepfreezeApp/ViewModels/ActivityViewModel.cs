using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;

using Caliburn.Micro;
using DeepfreezeModel;
using DeepfreezeSDK;
using DeepfreezeSDK.Exceptions;

namespace DeepfreezeApp
{
    [Export(typeof(IActivityViewModel))]
    public class ActivityViewModel : Screen, IActivityViewModel, IHandleWithTask<IFetchNotificationsMessage>
    {
        #region fields

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(ActivityViewModel));
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private BindableCollection<Notification> _notifications = new BindableCollection<Notification>();
        private string _errorMessage;
        private bool _hasNewNotifications = false;
        private string _nextPageUri = String.Empty;
        private bool _isBusy = false;
        private string _eTagPage1 = String.Empty;
        private string _noActivityText = String.Empty;
        DispatcherTimer _fetchNotificationsTimer;
        DispatcherTimer _fetchNotificationsAfterScrollToEndTimer;

        private const int INTERVAL_ACTIVITY_REFRESH = 5; // minutes
        private const int INTERVAL_ACTIVITY_SCROLL_FETCH = 500; // miliseconds

        #endregion

        #region constructor

        [ImportingConstructor]
        public ActivityViewModel(IDeepfreezeClient deepfreezeClient, IEventAggregator eventAggregator)
        {
            this._deepfreezeClient = deepfreezeClient;
            this._eventAggregator = eventAggregator;

            // get a new DispatcherTimer on the UI Thread.
            this._fetchNotificationsTimer = new DispatcherTimer(new TimeSpan(0, INTERVAL_ACTIVITY_REFRESH, 0), DispatcherPriority.Normal, Tick, Application.Current.Dispatcher);
            this._fetchNotificationsTimer.Start();

            this._fetchNotificationsAfterScrollToEndTimer = new DispatcherTimer(new TimeSpan(0, 0, 0, 0, INTERVAL_ACTIVITY_SCROLL_FETCH),
                                                                                        DispatcherPriority.Normal,
                                                                                        ScrollTick,
                                                                                        Application.Current.Dispatcher);
            this._fetchNotificationsAfterScrollToEndTimer.Stop();
        }

        #endregion

        #region properties

        public BindableCollection<Notification> Notifications
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

        public bool IsBusy
        {
            get { return this._isBusy; }
            set { this._isBusy = value; NotifyOfPropertyChange(() => this.IsBusy); }
        }

        public string NoActivityText
        {
            get { return Properties.Resources.NoActivityText; }
        }

        #endregion

        #region action_methods

        /// <summary>
        /// Set Notification.IsNew = false for all notifications in the Notifications BindableCollection.
        /// </summary>
        public void SetAllNotificationsAsRead()
        {
            // Iterate through a copy of the _notifications list because 
            // UpdateNotificationInCollection changes the collection,
            // so iterating through the original _notifications list would throw an exception.
            foreach (var notification in this._notifications.ToList())
            {
                notification.IsNew = false;
                this.UpdateNotificationInCollection(notification);
            }

            // check for new (tip: none) to change the red dot UI.
            this.UpdateHasNewNotifications();

            // Keep the most recent creation date to have a checkpoint for new notifications.
            if (this.Notifications.Count > 0)
            {
                var latestNotificationDate = this.Notifications.Max(x => x.CreationDate);
                this.SetLastNotificationDate(latestNotificationDate);
            }
        }

        /// <summary>
        /// This plays the role of LayoutUpdated handler of ListBox control.
        /// When the scrollviewer is scrolled at its end, then start a timer
        /// which will execute a FetchNotificationsAsync after a small interval.
        /// Also, if scrollviewer.VerticalOffset = scrollviewer.ScrollableHeight = 0
        /// then a fetch will occur. As a result, notifications will keep being fetched
        /// until the listbox is full (the scrollviewer is showed) inside the current window.
        /// </summary>
        /// <param name="sender"></param>
        public void PrepareToFetchWhenScrollStopsAtEnd(object sender)
        {
            if (this.IsBusy)
            {
                return;
            }

            var scrollviewer = ((System.Windows.Controls.ListBox)sender).GetDescendantByType<System.Windows.Controls.ScrollViewer>();

            if (scrollviewer == null)
            { 
                return; 
            }

            if (scrollviewer.VerticalOffset == scrollviewer.ScrollableHeight)
            {
                this._fetchNotificationsAfterScrollToEndTimer.Start();
            }
        }

        public void ForgetBeyondPageOneResults()
        {
            while(this.Notifications.Count > 10)
            {
                var not = this.Notifications.Last();
                this.Notifications.RemoveAt(this.Notifications.Count - 1);
                not = null;
            }
        }

        /// <summary>
        /// Navigate to the URI associated with a Notification object.
        /// Also, mark the notification as Read (IsNew = false). Finally,
        /// if that was the last unread notification, update the red dot UI
        /// next to the gear icon.
        /// </summary>
        /// <param name="notification"></param>
        public void OpenNotificationUrl(Notification notification)
        {
            // Since Notification doesn't implement INPC, we can't just update its IsNew property
            // and expect to see the UI change. So we have to find the original position of the 
            // notification in the notifications list and replace it with the one passed as a parameter.

            // set the notification's IsNew to false;
            notification.IsNew = false;

            this.UpdateNotificationInCollection(notification);

            if (!String.IsNullOrEmpty(notification.Url))
            {
                var uri = new Uri(notification.Url, UriKind.Absolute);

                if (!uri.IsFile && !uri.IsUnc && uri.IsWellFormedOriginalString() &&
                    (uri.Scheme == "https" || uri.Scheme == "http"))
                {
                    Process.Start(notification.Url);
                }
            }

            // finally check if all notifications are read to update the UI.
            this.UpdateHasNewNotifications();
        }

        #endregion

        #region private_methods

        /// <summary>
        /// Fetches Notification objects by requesting the notifications URI.
        /// If a previous fetch has been completed and a next page result exists,
        /// then the next fetch will get the next page results etc.
        /// Optional
        /// page: The page to fetch.
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        private async Task FetchNotificationsAsync(int page = 0)
        {
            this.IsBusy = true;
            this.ErrorMessage = null;
            string url = String.Empty;

            // if page 1 is not requested, then try to fetch the next page.
            if (page != 1)
            {
                url = _nextPageUri;
            }

            try
            {
                var notificationsTuple = await this._deepfreezeClient.GetNotificationsAsync(url).ConfigureAwait(false);

                // if the result is null, then there was nothing to fetch
                // Update the UI and return.
                if (notificationsTuple == null)
                {
                    //this.NoActivityText = Properties.Resources.NoActivityText;
                    return;
                }

                // get the response metadata
                var responseMetadata = notificationsTuple.Item1;

                // if page == 1 then we need to check for etag change
                // if the page 1 etag is null then set it, since this is obviously the 1st time you fetched notifications
                if (page == 1)
                {
                    if (String.IsNullOrEmpty(this._eTagPage1))
                    {
                        this._eTagPage1 = responseMetadata.Etag;
                    }
                    // else check if page 1 etag has the same value with the one found in the response metadata
                    else
                    {
                        // if so, just return
                        if (this._eTagPage1 == responseMetadata.Etag)
                        {
                            return;
                        }
                    }
                }

                // set the next page results uri
                this._nextPageUri = responseMetadata.NextPageUri;

                responseMetadata = null;

                // get the notifications result
                var notifications = notificationsTuple.Item2.ToList();

                // foreach result, create a new Notification object and try marking it as unread.
                // finally add it to the Notifications list.
                foreach (var notification in notifications)
                {
                    this.SetUnreadStatusInNotification(notification);

                    this.Notifications.Add(notification);
                }

                notifications.Clear();
                notificationsTuple = null;

                // finally try updating the new notifications flag
                this.UpdateHasNewNotifications();
            }
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\"." +
                           BigStashExceptionHelper.TryGetBigStashExceptionInformation(e), e);

                this.ErrorMessage = Properties.Resources.ErrorFetchingActivityGenericText;
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        private void SetUnreadStatusInNotification(Notification notification)
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

            if (mostRecentDate >= notification.CreationDate)
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

        private void UpdateNotificationInCollection(Notification notification)
        {
            // find the index of the notification in the notifications list.
            // we have to match the id to find the notification in the list.
            var index = this._notifications.IndexOf(this._notifications.Where(x => x.Id == notification.Id).FirstOrDefault());

            // replace the notification in the list at the found index
            // with the notification given as a parameter, which has IsNew = false;
            this.Notifications.RemoveAt(index);
            this.Notifications.Insert(index, notification);
        }

        #endregion

        #region message_handlers

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
        
        private async void Tick(object sender, EventArgs e)
        {
            await this.FetchNotificationsAsync(1).ConfigureAwait(false); ;
        }

        private async void ScrollTick(object sender, EventArgs e)
        {
            this._fetchNotificationsAfterScrollToEndTimer.Stop();

            await this.FetchNotificationsAsync().ConfigureAwait(false);
        }

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
