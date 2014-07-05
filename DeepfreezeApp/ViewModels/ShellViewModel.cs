using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using Caliburn.Micro;
using DeepfreezeSDK;
using MahApps.Metro.Controls;

namespace DeepfreezeApp
{
    [Export(typeof(IShell))]
    public class ShellViewModel : Screen, IShell, IHandle<ILoginSuccessMessage>
    {
        #region fields
        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private ArchiveViewModel _archiveVM;
        private LoginViewModel _loginVM = IoC.Get<ILoginViewModel>() as LoginViewModel;
        private PreferencesViewModel _preferencesVM;

        private MetroWindow _shellWindow;
        private bool _isPreferencesFlyoutOpen = false;
        #endregion

        #region properties
        public MetroWindow ShellWindow
        {
            get { return this._shellWindow; }
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
        }

        public PreferencesViewModel PreferencesVM
        {
            get { return this._preferencesVM; }
            set { this._preferencesVM = value; NotifyOfPropertyChange(() => PreferencesVM); }
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
        public void TogglePreferencesFlyout()
        {
            //ShellWindow.ShowWindowCommandsOnTop = IsPreferencesFlyoutOpen;
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

            //IsLoggedIn = _deepfreezeClient.IsLogged();
        }
        #endregion

        #region message handlers
        public void Handle(ILoginSuccessMessage message)
        {
            NotifyOfPropertyChange(() => IsLoggedIn);
            GetLoggedInViewModels();
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

        protected override async void OnActivate()
        {
            try
            {
                if (IsLoggedIn)
                {
                    GetLoggedInViewModels();
                    var user = await this._deepfreezeClient.GetUserAsync();
                    
                    if (user != null)
                        this._deepfreezeClient.Settings.ActiveUser = user;
                }

            }
            catch(Exception e)
            {

            }

            base.OnActivate();
        }
        #endregion

        #region private methods

        private void GetLoggedInViewModels()
        {
            if (ArchiveVM == null)
            {
                ArchiveVM = IoC.Get<IArchiveViewModel>() as ArchiveViewModel;
            }

            if (PreferencesVM == null)
            {
                PreferencesVM = IoC.Get<IPreferencesViewModel>() as PreferencesViewModel;
            }
        }

        #endregion
    }
}
