using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Drawing;

using Caliburn.Micro;
using DeepfreezeSDK;
using MahApps.Metro.Controls;
using DeepfreezeModel;
using System.IO;
using Newtonsoft.Json;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;

namespace DeepfreezeApp
{
    [Export(typeof(IShell))]
    public class ShellViewModel : Conductor<Object>.Collection.AllActive, IShell, IHandle<ILoginSuccessMessage>, IHandle<ILogoutMessage>,
        IHandle<INotificationMessage>, IHandle<IStartUpArgsMessage>
    {
        #region fields

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(ShellViewModel));
        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private ArchiveViewModel _archiveVM;
        private LoginViewModel _loginVM;
        private PreferencesViewModel _preferencesVM;
        private AboutViewModel _aboutVM;
        private UploadManagerViewModel _uploadManagerVM;

        private MetroWindow _shellWindow;
        private TaskbarIcon _tray;
        private bool _isPreferencesFlyoutOpen = false;
        private bool _isAboutFlyoutOpen = false;

        private bool _isBusy = false;
        private bool _hasError = false;
        private string _busyMessage;
        private string _errorMessage;
        private bool _trayExitClicked = false;
        private bool _minimizeBallonTipShown = false;

        private DispatcherTimer _connectionTimer;
        private bool _isInternetConnected = true;

        #endregion

        #region properties

        public MetroWindow ShellWindow
        {
            get { return this._shellWindow; }
        }

        public bool IsBusy
        {
            get { return this._isBusy; }
            set { this._isBusy = value; NotifyOfPropertyChange(() => IsBusy); }
        }

        public bool HasError
        {
            get { return this._hasError; }
            set { this._hasError = value; NotifyOfPropertyChange(() => HasError); }
        }

        public string BusyMessage
        {
            get { return this._busyMessage; }
            set { this._busyMessage = value; NotifyOfPropertyChange(() => BusyMessage); }
        }

        public string ErrorMessage
        {
            get { return this._errorMessage; }
            set { this._errorMessage = value; NotifyOfPropertyChange(() => ErrorMessage); }
        }

        public bool IsLoggedIn
        { get { return this._deepfreezeClient.IsLogged(); } }
        
        public ArchiveViewModel ArchiveVM
        {
            get { return this._archiveVM; }
            set { this._archiveVM = value; NotifyOfPropertyChange(() => ArchiveVM); }
        }
        
        public LoginViewModel LoginVM
        {
            get { return this._loginVM; }
            set { this._loginVM = value; NotifyOfPropertyChange(() => LoginVM); }
        }

        public PreferencesViewModel PreferencesVM
        {
            get { return this._preferencesVM; }
            set { this._preferencesVM = value; NotifyOfPropertyChange(() => PreferencesVM); }
        }

        public AboutViewModel AboutVM
        {
            get { return this._aboutVM; }
            set { this._aboutVM = value; NotifyOfPropertyChange(() => AboutVM); }
        }

        public UploadManagerViewModel UploadManagerVM
        {
            get { return this._uploadManagerVM; }
            set { this._uploadManagerVM = value; NotifyOfPropertyChange(() => UploadManagerVM); }
        }

        public bool IsPreferencesFlyoutOpen
        {
            get { return this._isPreferencesFlyoutOpen; }
            set { this._isPreferencesFlyoutOpen = value; NotifyOfPropertyChange(() => IsPreferencesFlyoutOpen); }
        }

        public bool IsAboutFlyoutOpen
        {
            get { return this._isAboutFlyoutOpen; }
            set { this._isAboutFlyoutOpen = value; NotifyOfPropertyChange(() => IsAboutFlyoutOpen); }
        }

        public string PreferencesHeader
        { get { return Properties.Resources.PreferencesHeader; } }

        public string AboutHeader
        { get { return Properties.Resources.AboutHeader; } }

        public string AboutButtonTooltip
        { get { return Properties.Resources.AboutButtonTooltip; } }

        public string ExitHeader
        { get { return Properties.Resources.ExitHeader; } }

        public bool IsInternetConnected
        {
            get { return this._isInternetConnected; }
            set { this._isInternetConnected = value; NotifyOfPropertyChange(() => IsInternetConnected); }
        }

        #endregion

        #region action methods

        /// <summary>
        /// Toggle Preferences Flyout IsOpen property.
        /// </summary>
        public void TogglePreferencesFlyout()
        {
            IsPreferencesFlyoutOpen = !IsPreferencesFlyoutOpen;

            if (IsPreferencesFlyoutOpen)
            {
                IsAboutFlyoutOpen = false;
            }
        }

        public void ToggleAboutFlyout()
        {
            IsAboutFlyoutOpen = !IsAboutFlyoutOpen;

            if (IsAboutFlyoutOpen)
            {
                IsPreferencesFlyoutOpen = false;

                // send a message with the aggregator to check for updates
                // only if the user has automatic updates settings enabled.\
                if (Properties.Settings.Default.DoAutomaticUpdates)
                {
                    var checkForUpdateMessage = IoC.Get<ICheckForUpdateMessage>() as CheckForUpdatesMessage;
                    this._eventAggregator.PublishOnUIThread(checkForUpdateMessage);
                }
            }
        }

        public void ShowShellWindow()
        {
            _shellWindow.ShowInTaskbar = true;
            _shellWindow.WindowState = WindowState.Normal;
            _shellWindow.Visibility = Visibility.Visible;
            _shellWindow.Activate();
        }

        public void ShowPreferences()
        {
            if (_shellWindow.WindowState == WindowState.Minimized)
                this.ShowShellWindow();

            if (!_shellWindow.IsActive)
                _shellWindow.Activate();

            if (!IsPreferencesFlyoutOpen)
                this.TogglePreferencesFlyout();
        }

        public void ExitApplication()
        {
            this._trayExitClicked = true;
            this.TryClose();
        }

        #endregion

        #region constructors

        public ShellViewModel() { }

        [ImportingConstructor]
        public ShellViewModel(IWindowManager windowManager, IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._windowManager = windowManager;
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;

            this._eventAggregator.Subscribe(this);

            // initialize the _connectionTimer
            this._connectionTimer = new DispatcherTimer();
            this._connectionTimer.Interval = new TimeSpan(0, 0, 5);
            this._connectionTimer.Tick += _connectionTimer_Tick;
            this._connectionTimer.Start();
        }

        #endregion

        #region message handlers

        /// <summary>
        /// Handle LoginSuccessMessage.
        /// </summary>
        /// <param name="message"></param>
        public void Handle(ILoginSuccessMessage message)
        {
            NotifyOfPropertyChange(() => IsLoggedIn);

            this.CloseItem(this.LoginVM);
            this.LoginVM = null;

            InstatiateArchiveViewModel();
            InstatiatePreferencesViewModel();
            InstatiateAboutViewModel();
            InstatiateUploadManagerViewModel();
        }

        /// <summary>
        /// Handle LogoutMessage
        /// </summary>
        /// <param name="message"></param>
        public void Handle(ILogoutMessage message)
        {
            this.Disconnect();
        }

        /// <summary>
        /// Handle NotificationMessage
        /// </summary>
        /// <param name="message"></param>
        public void Handle(INotificationMessage message)
        {
            if (message != null)
            {
                _tray.ShowBalloonTip(Properties.Settings.Default.ApplicationFullName, message.Message, BalloonIcon.Info);
            }
        }

        /// <summary>
        /// Handle StartUpArgsMessage
        /// </summary>
        /// <param name="message"></param>
        public void Handle(IStartUpArgsMessage message)
        {
            if (message != null)
            {
                switch(message.StartUpArgument)
                {
                    case "minimized":
                        
                        _shellWindow.WindowState = WindowState.Minimized;
                        _shellWindow.ShowInTaskbar = false;
                        break;
                    default:
                        
                        _shellWindow.WindowState = WindowState.Normal;
                        _shellWindow.ShowInTaskbar = true;
                        break;
                }
            }
        }

        #endregion

        #region events

        protected override void OnViewLoaded(object view)
        {
            var v = view as MetroWindow;
            v.Title = Properties.Settings.Default.ApplicationName;

            if (v != null)
            {
                _shellWindow = v;
                _tray = _shellWindow.FindName("DFTrayIcon") as TaskbarIcon;
            }

            base.OnViewLoaded(view);
        }

        /// <summary>
        /// Override the default OnActivate handler to setup the DeepfreezeClient for the first time.
        /// Also, based on the client's IsLogged method, instatiate the correct viewmodels.
        /// </summary>
        protected override async void OnActivate()
        {
            try
            {
                // first try to read local preferences file
                if (File.Exists(Properties.Settings.Default.SettingsFilePath))
                {
                    _log.Info("Reading preferences.json at \"" + Properties.Settings.Default.SettingsFilePath + "\".");

                    var content = File.ReadAllText(Properties.Settings.Default.SettingsFilePath, Encoding.UTF8);

                    if (!(String.IsNullOrEmpty(content) || String.IsNullOrWhiteSpace(content)))
                    {
                        this._deepfreezeClient.Settings = JsonConvert.DeserializeObject<Settings>(content);
                    }
                    else
                    {
                        _log.Warn("Preferences is null. Use default server address: \"" + Properties.Settings.Default.ServerBaseAddress + "\".");
                        var settings = new Settings();
                        this._deepfreezeClient.Settings = settings;
                    }
                }
                else
                {
                    _log.Info("Preferences.json doesn't exist. The client is disconnected.");

                    var settings = new Settings();
                    this._deepfreezeClient.Settings = settings;
                }

                // try to read the endpoint file
                if (File.Exists(Properties.Settings.Default.EndpointFilePath))
                {
                    var content = File.ReadAllText(Properties.Settings.Default.EndpointFilePath, Encoding.UTF8);

                    _log.Info("Found " + Properties.Settings.Default.EndpointFileName + 
                        " at \"" + Properties.Settings.Default.EndpointFilePath + 
                        "\" with value = \"" + content + "\".");

                    if (!(String.IsNullOrEmpty(content) ||
                          String.IsNullOrWhiteSpace(content)))
                    {
                        this._deepfreezeClient.Settings.ApiEndpoint = content;
                    }
                    else
                    {
                        _log.Warn("Endpoint is null. Setting to default as \"" + Properties.Settings.Default.ServerBaseAddress + "\".");

                        this._deepfreezeClient.Settings.ApiEndpoint = Properties.Settings.Default.ServerBaseAddress;
                    }
                }
                else
                {
                    // endpoint.txt does not exist, so set the base address to the Application wide default setting 
                    // 'ServerBaseAddress' variable.
                    _log.Info("Endpoint file doesn't exist. Setting to default as \"" + Properties.Settings.Default.ServerBaseAddress + "\".");
                    this._deepfreezeClient.Settings.ApiEndpoint = Properties.Settings.Default.ServerBaseAddress;

                    // try call SetDebugApiEndpoint(), which exists only in debug mode
                    // to override the above 'ServerBaseAddress'.
                    this.SetDebugApiEndpoint();
                }

                if (IsLoggedIn)
                {
                    this.IsBusy = true;
                    this.BusyMessage = "Validating user...";

                    var user = await this._deepfreezeClient.GetUserAsync();

                    if (user != null)
                    {
                        this.IsBusy = false;
                        this.BusyMessage = null;

                        _log.Info("Fetched User is valid, saving to \"" + Properties.Settings.Default.SettingsFilePath + "\".");

                        this._deepfreezeClient.Settings.ActiveUser = user;
                        // Save preferences file here again to sync with online data (quota, display name etc.).
                        LocalStorage.WriteJson(Properties.Settings.Default.SettingsFilePath, this._deepfreezeClient.Settings, Encoding.UTF8);

                        InstatiateArchiveViewModel();
                        InstatiatePreferencesViewModel();
                        InstatiateAboutViewModel();
                        InstatiateUploadManagerViewModel();
                    }
                    else
                    {
                        _log.Warn("GetUserAsync returned null.");
                        InstatiateLoginViewModel();
                    }
                }
                else
                {
                    InstatiateLoginViewModel();
                }
            }
            catch(JsonException e)
            {
                _log.Error("ShellViewModel.OnActivate threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");

                // The preferences file format seems to be invalid.
                var settings = new Settings();
                this._deepfreezeClient.Settings = settings;
                this._deepfreezeClient.Settings.ApiEndpoint = Properties.Settings.Default.ServerBaseAddress;

                InstatiateLoginViewModel();
            }
            catch(Exception e)
            {
                HasError = true;
                this.ErrorMessage = Properties.Resources.ErrorInitializingShellViewModelGenericText;

                // for every exception other than DfApiException update the error log.
                _log.Error("ShellViewModel's OnActivate threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");

                if (e is Exceptions.DfApiException)
                {
                    var response = ((Exceptions.DfApiException)e).HttpResponse;

                    switch (response.StatusCode)
                    {
                        case System.Net.HttpStatusCode.Unauthorized:
                        case System.Net.HttpStatusCode.Forbidden:
                            HasError = false;
                            this.ErrorMessage = null;
                            this.Disconnect("Your previous session is no longer valid. Please connect again.");
                            break;
                    }
                }
                else
                {
                    
                }
            }
            finally 
            { 
                IsBusy = false;

                base.OnActivate();
            }
        }

        protected override void OnDeactivate(bool close)
        {
            if (this._tray != null)
            {
                this._tray.Visibility = Visibility.Collapsed;
                this._tray.Dispose();
            }
            base.OnDeactivate(close);
        }

        public override void CanClose(Action<bool> callback)
        {
            // if the user selected exit from the tray icon, then always close.
            if (this._trayExitClicked)
            {
                callback(true);
            }
            else
            {
                // if the user clicked the close button, then check the MinimizeOnClose setting
                if (Properties.Settings.Default.MinimizeOnClose)
                {
                    _shellWindow.WindowState = WindowState.Minimized;
                    _shellWindow.ShowInTaskbar = false;

                    // if no user is currently connected, show a BallonTip
                    // informing the user about the application minimizing instead of exiting.
                    // Do this only one time in each application run.
                    if (!this._minimizeBallonTipShown)
                    {
                        this._minimizeBallonTipShown = true;
                        _tray.ShowBalloonTip(Properties.Settings.Default.ApplicationFullName, Properties.Resources.MinimizedMessageText, BalloonIcon.Info);
                    }

                    callback(false); // will cancel close
                }
                else
                    callback(true);
            }
        }

        /// <summary>
        /// Handle _connectionTimer ticks and publish messages
        /// for upload pause/resume on internet connectivity changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _connectionTimer_Tick(object sender, EventArgs e)
        {
            var isConnected = this._deepfreezeClient.IsInternetConnected;

            var uploadManagerVM = IoC.Get<IUploadManagerViewModel>() as UploadManagerViewModel;

            bool connectionStatusChanged = this.IsInternetConnected != isConnected;
            this.IsInternetConnected = isConnected;

            if (connectionStatusChanged)
            {
                var internetConnectivityMessage = IoC.Get<IInternetConnectivityMessage>();
                internetConnectivityMessage.IsConnected = this.IsInternetConnected;

                if (this.IsInternetConnected)
                {
                    _log.Warn(Properties.Resources.ConnectionRestoredMessage);

                    int autoPausedUploadsCount = uploadManagerVM.Uploads.Where(x => !x.LocalUpload.UserPaused && x.Upload.Status == Enumerations.Status.Pending).Count();

                    if (autoPausedUploadsCount > 0)
                        this._tray.ShowBalloonTip(Properties.Settings.Default.ApplicationFullName, Properties.Resources.ConnectionRestoredMessage, BalloonIcon.Info);
                }
                else
                {
                    _log.Warn(Properties.Resources.ConnectionLostMessage);

                    int autoPausedUploadsCount = uploadManagerVM.Uploads.Where(x => x.IsUploading).Count();

                    if (autoPausedUploadsCount > 0)
                        this._tray.ShowBalloonTip(Properties.Settings.Default.ApplicationFullName, Properties.Resources.ConnectionLostMessage, BalloonIcon.Warning);
                }

                this._eventAggregator.PublishOnUIThread(internetConnectivityMessage);
            }
        }

        #endregion

        #region private methods

        /// <summary>
        /// Instatiate a new LoginViewModel and activate it.
        /// </summary>
        private void InstatiateLoginViewModel(string errorMessage = null)
        {
            if (LoginVM == null)
            {
                LoginVM = IoC.Get<ILoginViewModel>() as LoginViewModel;
            }
            LoginVM.HasLoginError = true;
            LoginVM.LoginError = errorMessage;
            this.ActivateItem(LoginVM);
        }

        /// <summary>
        /// Instatiate a new ArchiveViewModel and activate it.
        /// </summary>
        private void InstatiateArchiveViewModel()
        {
            if (ArchiveVM == null)
            {
                ArchiveVM = IoC.Get<IArchiveViewModel>() as ArchiveViewModel;
            }
            this.ActivateItem(ArchiveVM);
        }

        /// <summary>
        /// Instatiate a new PreferencesViewModel and activate it.
        /// </summary>
        private void InstatiatePreferencesViewModel()
        {
            if (PreferencesVM == null)
            {
                PreferencesVM = IoC.Get<IPreferencesViewModel>() as PreferencesViewModel;
            }
            this.ActivateItem(PreferencesVM);
        }

        /// <summary>
        /// Instatiate a new AboutViewModel and activate it.
        /// </summary>
        private void InstatiateAboutViewModel()
        {
            if (AboutVM == null)
            {
                AboutVM = IoC.Get<IAboutViewModel>() as AboutViewModel;
            }
            this.ActivateItem(AboutVM);
        }

        /// <summary>
        /// Instatiate a new UploadManagerViewModel and activate it.
        /// </summary>
        private void InstatiateUploadManagerViewModel()
        {
            if (UploadManagerVM == null)
            {
                UploadManagerVM = IoC.Get<IUploadManagerViewModel>() as UploadManagerViewModel;
            }
            this.ActivateItem(UploadManagerVM);
        }

        /// <summary>
        /// Disconnect the current user for the DeepfreezeClient. This method closes 
        /// the preferences flyout, nullifies the DeepfreezeClient's ActiveUser and ActiveToken,
        /// deletes the local preferences file and handles the closing of each viewmodel that
        /// needs the user to be connected in order to be active. Finally it instatiates 
        /// a new LoginViewModel so the user can try connecting again.
        /// </summary>
        /// <param name="errorMessage"></param>
        private void Disconnect(string errorMessage = null)
        {
            // Close the preferences flyout.
            if (this.IsPreferencesFlyoutOpen)
                TogglePreferencesFlyout();

            _log.Info("Disconnecting user, deleting \"" + Properties.Settings.Default.SettingsFilePath + "\".");

            // When logging out, we delete the the local preferences file and reset
            // DeepfreezeClient's settings to null, so no user is logged in.
            this._deepfreezeClient.Settings.ActiveUser = null;
            this._deepfreezeClient.Settings.ActiveToken = null;
            File.Delete(Properties.Settings.Default.SettingsFilePath);

            NotifyOfPropertyChange(() => IsLoggedIn);

            this.CloseItem(this.ArchiveVM);
            this.CloseItem(this.PreferencesVM);
            this.CloseItem(this.UploadManagerVM);

            this.ArchiveVM = null;
            this.PreferencesVM = null;
            this.UploadManagerVM = null;

            InstatiateLoginViewModel(errorMessage);
        }

        /// <summary>
        /// Use this method to change the api endpoint for debug only mode.
        /// This method is called in the 'OnActivate' event and changes the api endpoint
        /// after the standard way of checking and saving the preferences.djf file,
        /// which includes a call to save that file before going on with the initialization.
        /// </summary>
        [Conditional("DEBUG")]
        private void SetDebugApiEndpoint()
        {
            // set the api endpoint for debug only mode
            // only if the user setting 'DebugServerBaseAddress' is set.
            string debugEndpoint = Properties.Settings.Default.DebugServerBaseAddress;

            if (!(String.IsNullOrEmpty(debugEndpoint) ||
                  String.IsNullOrWhiteSpace(debugEndpoint)))
            {
                MessageBox.Show("Setting custom endpoint to: " + debugEndpoint);
                this._deepfreezeClient.Settings.ApiEndpoint = Properties.Settings.Default.DebugServerBaseAddress;
            }
        }

        #endregion
    }
}
