using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using DeepfreezeSDK;
using DeepfreezeSDK.Exceptions;
using DeepfreezeModel;
using Caliburn.Micro;
using MahApps.Metro.Controls;
using Newtonsoft.Json;
using Hardcodet.Wpf.TaskbarNotification;

namespace DeepfreezeApp
{
    [Export(typeof(IShell))]
    public class ShellViewModel : Conductor<Screen>.Collection.AllActive, IShell, IHandle<ILoginSuccessMessage>, IHandle<ILogoutMessage>,
        IHandle<INotificationMessage>, IHandleWithTask<IStartUpArgsMessage>, IHandle<IRestartNeededMessage>, IHandleWithTask<IRestartAppMessage>
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
        private ActivityViewModel _activityVM;

        private MetroWindow _shellWindow;
        private TaskbarIcon _tray;
        private bool _isPreferencesFlyoutOpen = false;
        private bool _isAboutFlyoutOpen = false;
        private bool _isActivityFlyoutOpen = false;

        private bool _isBusy = false;
        private bool _hasError = false;
        private string _busyMessage;
        private string _errorMessage;
        private bool _trayExitClicked = false;

        private DispatcherTimer _connectionTimer;
        private bool _isInternetConnected = true;
        private bool _restartNeeded = false;
        private string _trayToolTipText = default(string);

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

        public ActivityViewModel ActivityVM
        {
            get { return this._activityVM; }
            set { this._activityVM = value; NotifyOfPropertyChange(() => this.ActivityVM); }
        }

        public bool IsPreferencesFlyoutOpen
        {
            get { return this._isPreferencesFlyoutOpen; }
            set { this._isPreferencesFlyoutOpen = value; NotifyOfPropertyChange(() => IsPreferencesFlyoutOpen); }
        }

        public bool IsAboutFlyoutOpen
        {
            get { return this._isAboutFlyoutOpen; }
            set 
            { 
                this._isAboutFlyoutOpen = value; 
                NotifyOfPropertyChange(() => this.IsAboutFlyoutOpen);
                NotifyOfPropertyChange(() => this.ShowRestartNeeded);
            }
        }

        public bool IsActivityFlyoutOpen
        {
            get { return this._isActivityFlyoutOpen; }
            set
            {
                if (!value && this.IsActivityFlyoutOpen)
                {
                    if (this.ActivityVM != null)
                    {
                        this.ActivityVM.SetAllNotificationsAsRead();
                    }
                    // this.ActivityVM.ForgetBeyondPageOneResults();
                }

                this._isActivityFlyoutOpen = value;
                NotifyOfPropertyChange(() => this.IsActivityFlyoutOpen);
            }
        }

        public string PreferencesHeader
        { get { return Properties.Resources.PreferencesHeader; } }

        public string AboutHeader
        { get { return Properties.Resources.AboutHeader; } }

        public string AboutButtonTooltip
        { get { return Properties.Resources.AboutButtonTooltip; } }

        public string ActivityHeader
        { get { return Properties.Resources.ActivityHeader; } }

        public string ExitHeader
        { get { return Properties.Resources.ExitHeader; } }

        public bool IsInternetConnected
        {
            get { return this._isInternetConnected; }
            set { this._isInternetConnected = value; NotifyOfPropertyChange(() => IsInternetConnected); }
        }

        public bool ShowRestartNeeded
        {
            get 
            {
                if (this.IsAboutFlyoutOpen)
                    return false;
                else
                    return this._restartNeeded; 
            }
        }

        public string UpdateFoundButtonTooltipText
        { get { return Properties.Resources.UpdateFoundButtonTooltipText; } }

        public string HelpHeader
        { get { return Properties.Resources.HelpHeader; } }

        public string HelpHeaderTooltipText
        { get { return Properties.Resources.HelpHeaderTooltip; } }

        public string TrayToolTipText
        {
            get { return this._trayToolTipText; }
            set
            {
                this._trayToolTipText = value;
                NotifyOfPropertyChange(() => this.TrayToolTipText);
            }
        }

        #endregion

        #region action methods

        /// <summary>
        /// Toggle Preferences Flyout IsOpen property.
        /// </summary>
        public void TogglePreferencesFlyout()
        {
            this.IsPreferencesFlyoutOpen = !this.IsPreferencesFlyoutOpen;

            if (this.IsPreferencesFlyoutOpen)
            {
                this.IsAboutFlyoutOpen = false;
                this.IsActivityFlyoutOpen = false;
            }
        }

        public void ToggleAboutFlyout()
        {
            this.IsAboutFlyoutOpen = !this.IsAboutFlyoutOpen;

            this.AboutVM.TabSelectedIndex = 0;

            if (this.IsAboutFlyoutOpen)
            {
                this.IsPreferencesFlyoutOpen = false;
                this.IsActivityFlyoutOpen = false;
            }
        }

        public void ToggleActivityFlyout()
        {
            this.IsActivityFlyoutOpen = !this.IsActivityFlyoutOpen;

            if (this.IsActivityFlyoutOpen)
            {
                this.IsAboutFlyoutOpen = false;
                this.IsPreferencesFlyoutOpen = false;

                // When not busy (already fetching notifications),
                // send a message to fetch the latest notifications
                if (!this.ActivityVM.IsBusy)
                {
                    var fetchNotificationsMessage = IoC.Get<IFetchNotificationsMessage>();
                    fetchNotificationsMessage.PagedResult = 1;
                    this._eventAggregator.PublishOnUIThreadAsync(fetchNotificationsMessage);
                }
            }
            else
            {
                
            }
        }

        public void ShowOptionsContextMenu(object sender)
        {
            InstatiatePreferencesViewModel();
            InstatiateNotificationsViewModel();
            InstatiateAboutViewModel();
            
            var button = sender as System.Windows.Controls.Button;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.VerticalOffset = 5;
            button.ContextMenu.IsOpen = true;
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

        public void OpenHelp()
        {
            Process.Start(Properties.Settings.Default.BigStashSupportURL);
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
            this.Disconnect(shouldDeletePreferences: true);
        }

        /// <summary>
        /// Handle NotificationMessage
        /// </summary>
        /// <param name="message"></param>
        public void Handle(INotificationMessage message)
        {
            if (message != null)
            {
                var icon = message.NotificationStatus == Enumerations.NotificationStatus.Error
                    ? BalloonIcon.Error
                    : BalloonIcon.Info;

                if (!this._shellWindow.IsActive)
                {
                    _tray.ShowBalloonTip(Properties.Settings.Default.ApplicationFullName, message.Message, icon);
                }
            }
        }

        /// <summary>
        /// Handle StartUpArgsMessage
        /// </summary>
        /// <param name="message"></param>
        public async Task Handle(IStartUpArgsMessage message)
        {
            if (message != null)
            {
                await this.WorkOnStartUpArgsAsync(message.StartUpArguments).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Handle RestartAppMessage
        /// </summary>
        /// <param name="message"></param>
        public void Handle(IRestartNeededMessage message)
        {
            if (message != null)
            {
                this._restartNeeded = message.RestartNeeded;
                NotifyOfPropertyChange(() => this.ShowRestartNeeded);
            }
        }

        public async Task Handle(IRestartAppMessage message)
        {
            if (message != null)
            {
                this._connectionTimer.Stop();

                if (message.DoGracefulRestart)
                {
                    // This will finally restart by using squirrels RestartApp which does not terminate gracefully. 
                    // So let's take care of gracefully stoping and saving uploads and settings before using it.
                    
                    //if (message.ConfigureSettingsMigration)
                    //{
                    //    this.ConfigureSettingsMigration();
                    //}

                    // OK let's deactivate all uploads.
                    await this.UploadManagerVM.DeactivateAllUploads(true);

                    while(this.Items.Count > 0)
                    {
                        this.DeactivateItem(this.Items.FirstOrDefault(), true);
                    }
                }

                Application.Current.Shutdown();
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
                        this._deepfreezeClient.Settings.ApiEndpoint = content
                            .TrimEnd(Environment.NewLine.ToCharArray());
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
                        var writeSuccess = LocalStorage.WriteJson(Properties.Settings.Default.SettingsFilePath, this._deepfreezeClient.Settings, Encoding.UTF8, true);

                        if (!writeSuccess)
                        {
                            await this._windowManager.ShowMessageViewModelAsync("There was an error while trying to save your settings. " + 
                                "Please make sure that you have enough free space on your hard drive.\n\n" + 
                                "You may have to reconnect your BigStash account the next time you run the BigStash application.", "Error saving settings", 
                                MessageBoxButton.OK);
                        }

                        InstatiateArchiveViewModel();
                        InstatiatePreferencesViewModel();
                        InstatiateAboutViewModel();
                        InstatiateUploadManagerViewModel();
                        InstatiateNotificationsViewModel();
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
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");

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

                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");

                if (e is BigStashException)
                {
                    var bgex = e as BigStashException;

                    switch (bgex.StatusCode)
                    {
                        // If the current token use returns unauthorized or forbidden
                        // then mark it for deletion.
                        case System.Net.HttpStatusCode.Unauthorized:
                        case System.Net.HttpStatusCode.Forbidden:
                            HasError = false;
                            this.ErrorMessage = null;
                            this.Disconnect(Properties.Resources.PreviousSessionNoLongerValidText);
                            break;
                    }
                }
            }
            finally 
            { 
                IsBusy = false;
                this.BusyMessage = null;
            }

            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            if (this._tray != null)
            {
                this._tray.Visibility = Visibility.Collapsed;
                this._tray.Dispose();
            }

            //if (this.ShowRestartNeeded)
            //{
            //    this.ConfigureSettingsMigration();
            //}

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
                    if (!Properties.Settings.Default.MinimizeBallonTipShown)
                    {
                        Properties.Settings.Default.MinimizeBallonTipShown = true;
                        Properties.Settings.Default.Save();
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

            UpdateTrayIconToolTipWithCurrentStatus();

            if (connectionStatusChanged)
            {
                var internetConnectivityMessage = IoC.Get<IInternetConnectivityMessage>();
                internetConnectivityMessage.IsConnected = this.IsInternetConnected;

                if (this.IsInternetConnected)
                {
                    _log.Warn(Properties.Resources.ConnectionRestoredMessage);

                    int autoPausedUploadsCount = uploadManagerVM.PendingUploads.Where(x => !x.LocalUpload.UserPaused && x.Upload.Status == Enumerations.Status.Pending).Count();

                    if (autoPausedUploadsCount > 0)
                        this._tray.ShowBalloonTip(Properties.Settings.Default.ApplicationFullName, Properties.Resources.ConnectionRestoredMessage, BalloonIcon.Info);
                }
                else
                {
                    _log.Warn(Properties.Resources.ConnectionLostMessage);

                    int autoPausedUploadsCount = uploadManagerVM.PendingUploads.Where(x => x.IsUploading).Count();

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

            if (!this.PreferencesVM.IsActive)
            {
                this.ActivateItem(PreferencesVM);
            }
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

            if (!this.AboutVM.IsActive)
            {
                this.ActivateItem(AboutVM);
            }
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
        /// Instatiate a new NotificationsViewModel and activate it.
        /// </summary>
        private void InstatiateNotificationsViewModel()
        {
            if (this.ActivityVM == null)
            {
                this.ActivityVM = IoC.Get<IActivityViewModel>() as ActivityViewModel;
            }

            if (!this.ActivityVM.IsActive)
            {
                this.ActivateItem(ActivityVM);
            }
        }

        /// <summary>
        /// Disconnect the current user for the DeepfreezeClient. This method closes 
        /// the preferences flyout, nullifies the DeepfreezeClient's ActiveUser and ActiveToken,
        /// deletes the local preferences file and handles the closing of each viewmodel that
        /// needs the user to be connected in order to be active. Finally it instatiates 
        /// a new LoginViewModel so the user can try connecting again.
        /// </summary>
        /// <param name="errorMessage"></param>
        private void Disconnect(string errorMessage = null, bool shouldDeletePreferences = false)
        {
            // Close the preferences flyout.
            if (this.IsPreferencesFlyoutOpen)
                TogglePreferencesFlyout();

            // When logging out, we delete the the local preferences file and reset
            // DeepfreezeClient's settings to null, so no user is logged in.
            this._deepfreezeClient.Settings.ActiveUser = null;
            this._deepfreezeClient.Settings.ActiveToken = null;

            if (shouldDeletePreferences)
            {
                _log.Info("Disconnecting user and deleting preferences file.");
                File.Delete(Properties.Settings.Default.SettingsFilePath);
            }
            else
            {
                _log.Info("Disconnecting user. Preferences file won't get deleted.");
            }
            

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

        /// <summary>
        /// Call SquirrelHelper.CopyMigrationUserConfig() to copy settings for migrating on the next run.
        /// </summary>
        /// <returns></returns>
        private string ConfigureSettingsMigration()
        {
            try
            {
                // Copy current user settings to migrate to.
                var migrationUserConfigPath = SquirrelHelper.CopyMigrationUserConfig();
                return migrationUserConfigPath;
            }
            catch(Exception)
            {
                _log.Error("Settings migration failed. Default settings will be used if the next instance is an update.");
                return null;
            }
        }

        private async Task WorkOnStartUpArgsAsync(string[] startUpArgs)
        {
            if (startUpArgs.Length == 0)
            {
                return;
            }

            _log.Debug("StarUp arguments arrived.");

            var args = startUpArgs.ToList();

            // Check for minimized argument.
            if (args.Contains("-m"))
            {
                _log.Debug("Got Argument: minimized");

                _shellWindow.WindowState = WindowState.Minimized;
                _shellWindow.ShowInTaskbar = false;
            }
            else
            {
                _shellWindow.WindowState = WindowState.Normal;
                _shellWindow.ShowInTaskbar = true;
            }

            // Check for -u argument. If it's followed by --fromfile,
            // then the 2nd next argument should be the path of a file
            // containing all selected file paths. Else, the next argument
            // should be string containing at least one path in its value,
            // delimited by the character '|'.
            if (args.Contains("-u"))
            {
                _log.Debug("Got Argument: -u");

                var indexOfU = args.IndexOf("-u");

                IList<string> paths;

                // If the next argument is --fromfile, then the following path is the path of the file
                // containing all paths from the user's selection.
                if (args[indexOfU + 1] == "--fromfile")
                {
                    var selectionFile = args[indexOfU + 2];

                    _log.Debug("Got Argument: --fromfile '" + selectionFile +"'");

                    if (!File.Exists(selectionFile))
                    {
                        MessageBox.Show("Could not get the files you selected to include in the archive.");
                        return;
                    }
                    else
                    {
                        try
                        {
                            var res = await Utilities.ReadPathsFromSelectionFileAsync(selectionFile).ConfigureAwait(false);
                            paths = res.ToList();

                            _log.Debug("Successfully read paths from file.");

                            File.Delete(selectionFile);
                            _log.Debug("Deleted file: '" + selectionFile + "'");
                        }
                        catch (Exception e)
                        {
                            _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");
                            return;
                        }
                    }
                }
                else
                {
                    _log.Debug("Got Argument: -u (without --fromfile");

                    try
                    {
                        var pathsArg = args[indexOfU + 1];

                        if (String.IsNullOrEmpty(pathsArg))
                        {
                            throw new IndexOutOfRangeException("Paths argument was null or empty.");
                        }

                        paths = new List<string>();
                        foreach (var path in pathsArg.Split('|'))
                        {
                            paths.Add(path);
                        }
                    }
                    catch(Exception e)
                    {
                        _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");

                        if (e is IndexOutOfRangeException)
                        {
                            MessageBox.Show("You didn't select any files to archive.");
                        }

                        return;
                    }
                }

                while(this.ArchiveVM == null || !this.ArchiveVM.IsActive)
                {
                    await Task.Delay(500).ConfigureAwait(false);
                }

                var createArchiveMessage = IoC.Get<ICreateArchiveMessage>();
                createArchiveMessage.Paths = paths.AsEnumerable();
                await this._eventAggregator.PublishOnUIThreadAsync(createArchiveMessage);
            }
        }

        private void UpdateTrayIconToolTipWithCurrentStatus()
        {
            if (!this.IsInternetConnected)
            {
                this.TrayToolTipText = "Connecting";
                return;
            }

            if (this.UploadManagerVM == null)
            {
                return;
            }

            if (this.UploadManagerVM.PendingUploads.Count > 0)
            {
                var uploadingCount = this.UploadManagerVM.PendingUploads
                    .Where(x => x.OperationStatus == Enumerations.Status.Uploading)
                    .Count();

                var pausedCount = this.UploadManagerVM.PendingUploads
                    .Where(x => x.OperationStatus == Enumerations.Status.Paused)
                    .Count();

                var errorCount = this.UploadManagerVM.PendingUploads
                    .Where(x => x.OperationStatus == Enumerations.Status.Error)
                    .Count();

                var sb = new StringBuilder();
                sb.Append(String.Format("Uploading: {0} - ", uploadingCount));
                sb.Append(String.Format("Paused: {0} - ", pausedCount));
                sb.Append(String.Format("Errors: {0}", errorCount));

                this.TrayToolTipText = sb.ToString();
            }
            else
            {
                this.TrayToolTipText = null;
            }
        }

        #endregion
    }
}
