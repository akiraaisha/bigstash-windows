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

        private IArchiveViewModel _archiveVM = IoC.Get<IArchiveViewModel>();
        private ILoginViewModel _loginVM = IoC.Get<ILoginViewModel>();
        private IPreferencesViewModel _preferencesVM = IoC.Get<IPreferencesViewModel>();

        private MetroWindow _shellWindow;
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
            get { return this._archiveVM as ArchiveViewModel; }
        }
        
        public LoginViewModel LoginVM
        {
            get { return this._loginVM as LoginViewModel; }
        }

        public PreferencesViewModel PreferencesVM
        {
            get { return this._preferencesVM as PreferencesViewModel; }
        }

        public bool IsPreferencesFlyoutOpen
        {
            get { return this.PreferencesVM.IsOpen; }
            set { this.PreferencesVM.IsOpen = value; NotifyOfPropertyChange(() => IsPreferencesFlyoutOpen); }
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
        #endregion
    }
}
