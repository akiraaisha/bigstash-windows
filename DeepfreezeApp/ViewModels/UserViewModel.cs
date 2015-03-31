using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using Caliburn.Micro;
using DeepfreezeSDK;
using BigStash.Model;
using System.IO;

namespace BigStash.WPF
{
    [Export(typeof(IUserViewModel))]
    public class UserViewModel : Screen, IUserViewModel, IHandleWithTask<IRefreshUserMessage>,
        IHandle<IInternetConnectivityMessage>
    {
        #region members

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(UserViewModel));
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private bool _isBusy = false;
        private bool _isBusyDisconnecting = false;
        private string _errorMessage;

        #endregion

        #region constructors

        public UserViewModel() { }

        [ImportingConstructor]
        public UserViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
        }

        #endregion

        #region properties

        public bool IsBusy
        {
            get { return this._isBusy; }
            set { this._isBusy = value; }
        }

        public string ErrorMessage
        {
            get { return this._errorMessage; }
            set { this._errorMessage = value; NotifyOfPropertyChange(() => this.ErrorMessage); }
        }

        public User ActiveUser
        {
            get { return this._deepfreezeClient.Settings.ActiveUser; }
        }

        public string ActiveUserHeader
        { get { return Properties.Resources.ActiveUserHeader; } }

        public string QuotaHeader
        { get { return Properties.Resources.QuotaHeader; } }

        public string LogoutString
        { get { return Properties.Resources.DisconnectButtonContent; } }

        public double UsedPercentage
        {
            get 
            {
                var percentage = ((double)this.ActiveUser.Quota.Used / this.ActiveUser.Quota.Size) * 100;
                return percentage;    
            }
        }

        public string SizeInformation
        { 
            get 
            {
                double used = (double)this.ActiveUser.Quota.Used;
                double total = (double)this.ActiveUser.Quota.Size;

                var sb = new StringBuilder();
                sb.Append(LongToSizeString.ConvertToString(total - used));
                sb.Append(Properties.Resources.FreeText);
                sb.Append(LongToSizeString.ConvertToString(used));
                sb.Append(Properties.Resources.UsedText);
                sb.Append(LongToSizeString.ConvertToString(total));
                sb.Append(Properties.Resources.TotalText);

                return sb.ToString();
            } 
        }

        public string RefreshButtonTooltipText
        {
            get
            {
                if (this.IsInternetConnected)
                    return Properties.Resources.RefreshButtonEnabledTooltipText;
                else
                    return Properties.Resources.RefreshButtonDisabledTooltipText;
            }
        }

        public bool IsInternetConnected
        {
            get { return this._deepfreezeClient.IsInternetConnected; }
        }

        public bool IsBusyDisconnecting
        {
            get { return this._isBusyDisconnecting; }
            set
            {
                this._isBusyDisconnecting = value;
                NotifyOfPropertyChange(() => this.IsBusyDisconnecting);
            }
        }

        #endregion

        #region action_methods

        public async Task RefreshUser()
        {
            this.Reset();

            try
            {
                if (!this._deepfreezeClient.IsInternetConnected)
                    throw new Exception("Can't refresh user stats without an active Internet connection.");

                this.IsBusy = true;

                _log.Info("Fetching User, GET users resource.");
                var user = await this._deepfreezeClient.GetUserAsync();

                if (user != null)
                {
                    _log.Info("Fetched User, saving to \"" + Properties.Settings.Default.SettingsFilePath + "\"");

                    this._deepfreezeClient.Settings.ActiveUser = user;
                    // Save preferences file here again to sync with online data (quota, display name etc.).
                    LocalStorage.WriteJson(Properties.Settings.Default.SettingsFilePath, this._deepfreezeClient.Settings, Encoding.UTF8);
                }
                else
                {
                    _log.Warn("GetUserAsync returned null. Disconnection will follow.");
                    // Publish a message to disconnect since the user is not valid.
                    // ShellViewModel is responsible for handling the message and activating the LoginViewModel.
                    this._eventAggregator.PublishOnCurrentThread(IoC.Get<ILogoutMessage>());
                }
            }
            catch (Exception e) 
            {
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");

                this.ErrorMessage = Properties.Resources.ErrorRefreshingUserStatsGenericText;
            }
            finally 
            { 
                this.IsBusy = false;
                this.Refresh();
            }
        }

        public async Task Disconnect()
        {
            this.IsBusyDisconnecting = true;

            _log.Info("User clicked Disconnect button from the preferences screen.");

            // Delete the active authorization BigStash token.
            try
            {
                await this._deepfreezeClient.DeleteTokenAsync(this._deepfreezeClient.Settings.ActiveToken);
            }
            catch(Exception e)
            {
                // If the token failed to delete, just log it.

                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");
            }
            finally
            {
                this.IsBusyDisconnecting = false;

                // Finally publish a message to notify for the login change.
                // ShellViewModel is responsible for handling the message and activating the LoginViewModel.
                this._eventAggregator.PublishOnUIThread(IoC.Get<ILogoutMessage>());
            }
        }

        #endregion

        #region message_handlers

        public async Task Handle(IRefreshUserMessage message)
        {
            await this.RefreshUser();
        }

        public void Handle(IInternetConnectivityMessage message)
        {
            if (message != null)
            {
                NotifyOfPropertyChange(() => this.IsInternetConnected);
                NotifyOfPropertyChange(() => this.RefreshButtonTooltipText);
            }
        }

        #endregion

        #region private_methods

        private void Reset()
        {
            this.ErrorMessage = null;
        }

        #endregion

        #region events

        protected override void OnActivate()
        {
            this._eventAggregator.Subscribe(this);

            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            this._eventAggregator.Unsubscribe(this);

            base.OnDeactivate(close);
        }

        #endregion
    }
}
