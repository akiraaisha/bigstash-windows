using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using Caliburn.Micro;
using DeepfreezeSDK;
using MahApps.Metro.Controls;
using DeepfreezeModel;
using System.IO;
using Newtonsoft.Json;

namespace DeepfreezeApp
{
    [Export(typeof(IShell))]
    public class ShellViewModel : Conductor<Object>.Collection.AllActive, IShell, IHandle<ILoginSuccessMessage>, IHandle<ILogoutMessage>
    {
        #region fields

        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private ArchiveViewModel _archiveVM;
        private LoginViewModel _loginVM;
        private PreferencesViewModel _preferencesVM;
        private UploadManagerViewModel _uploadManagerVM;

        private MetroWindow _shellWindow;
        private bool _isPreferencesFlyoutOpen = false;

        private bool _isBusy = false;
        private bool _hasError = false;
        private string _busyMessage;
        private string _errorMessage;

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

        public string PreferencesHeader
        { get { return Properties.Resources.PreferencesHeader; } }

        #endregion

        #region action methods

        /// <summary>
        /// Toggle Preferences Flyout IsOpen property.
        /// </summary>
        public void TogglePreferencesFlyout()
        {

            IsPreferencesFlyoutOpen = !IsPreferencesFlyoutOpen;
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

        #endregion

        #region events

        protected override void OnViewLoaded(object view)
        {
            var v = view as MetroWindow;
            v.Title = Properties.Settings.Default.ApplicationName;

            if (v != null)
                _shellWindow = v;

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
                    var content = File.ReadAllText(Properties.Settings.Default.SettingsFilePath, Encoding.ASCII);

                    if (content != null)
                    {
                        this._deepfreezeClient.Settings = JsonConvert.DeserializeObject<Settings>(content);

                        if (String.IsNullOrEmpty(this._deepfreezeClient.Settings.ApiEndpoint))
                            this._deepfreezeClient.Settings.ApiEndpoint = Properties.Settings.Default.ServerBaseAddress;
                    }
                    else
                    {
                        this._deepfreezeClient.Settings.ApiEndpoint = Properties.Settings.Default.ServerBaseAddress;
                    }
                }
                else
                {
                    var settings = new Settings();
                    settings.ApiEndpoint = Properties.Settings.Default.ServerBaseAddress;
                    this._deepfreezeClient.Settings = settings;
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

                        this._deepfreezeClient.Settings.ActiveUser = user;
                        // Save preferences file here again to sync with online data (quota, display name etc.).
                        LocalStorage.WriteJson(Properties.Settings.Default.SettingsFilePath, this._deepfreezeClient.Settings, Encoding.ASCII);

                        InstatiateArchiveViewModel();
                        InstatiatePreferencesViewModel();
                        InstatiateUploadManagerViewModel();
                    }
                }
                else
                {
                    InstatiateLoginViewModel();
                }
            }
            catch(Exception e)
            {
                if (e is Exceptions.DfApiException)
                {
                    var response = ((Exceptions.DfApiException)e).HttpResponse;

                    switch (response.StatusCode)
                    {
                        case System.Net.HttpStatusCode.Unauthorized:
                        case System.Net.HttpStatusCode.Forbidden:
                            this.Disconnect("Your previous session is no longer valid. Please connect again.");
                            break;
                    }
                }
                else
                {
                    HasError = true;
                    this.ErrorMessage = e.Message;
                }
            }
            finally 
            { 
                IsBusy = false;

                base.OnActivate();
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

        #endregion
    }
}
