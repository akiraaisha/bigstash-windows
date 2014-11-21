using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Deployment.Application;

using Caliburn.Micro;
using DeepfreezeSDK;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace DeepfreezeApp
{
    [Export(typeof(IPreferencesViewModel))]
    public class PreferencesViewModel : Conductor<Screen>.Collection.AllActive, IPreferencesViewModel
    {
        #region members

        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private IUserViewModel _userVM = IoC.Get<IUserViewModel>();

        private bool _isOpen;
        private string _errorMessage;
        private bool _runOnStartup;

        #endregion

        #region constructors
        public PreferencesViewModel() { }

        public PreferencesViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
        }
        #endregion

        #region properties
        public UserViewModel UserVM
        {
            get { return _userVM as UserViewModel; }
        }

        public bool IsOpen
        {
            get { return this._isOpen; }
            set { this._isOpen = value; NotifyOfPropertyChange(() => IsOpen); }
        }

        public string ApplicationNameHeader
        {
            get { return Properties.Settings.Default.ApplicationFullName; }
        }

        public string VersionText
        {
            get
            {
                if (ApplicationDeployment.IsNetworkDeployed)
                {
                    ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;
                    return Properties.Resources.VersionHeaderText + " " + ad.CurrentVersion.ToString();
                }
                else
                    return "Debugging mode";
            }
        }

        public string DebugHelpText
        {
            get { return Properties.Resources.DebugHelpText; }
        }

        public string DebugButtonContent
        {
            get { return Properties.Resources.DebugButtonContent; }
        }

        public string ErrorMessage
        {
            get { return this._errorMessage; }
            set { this._errorMessage = value; NotifyOfPropertyChange(() => this.ErrorMessage); }
        }

        public bool RunOnStartup
        {
            get { return this._runOnStartup; }
            set { this._runOnStartup = value; NotifyOfPropertyChange(() => this.RunOnStartup); }
        }

        public bool MinimizeOnClose
        {
            get { return Properties.Settings.Default.MinimizeOnClose; }
            set { Properties.Settings.Default.MinimizeOnClose = value; Properties.Settings.Default.Save(); NotifyOfPropertyChange(() => this.MinimizeOnClose); }
        }
        #endregion

        #region methods

        public void RunOnStartupChanged()
        {
            Microsoft.Win32.RegistryKey registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            Assembly curAssembly = Assembly.GetExecutingAssembly();

            if (this.RunOnStartup)
            {
                registryKey.SetValue(curAssembly.GetName().Name, curAssembly.Location + " minimized");
            }
            else
            {
                registryKey.DeleteValue(curAssembly.GetName().Name, false);
            }
        }

        #endregion

        #region events

        protected override void OnActivate()
        {
            Microsoft.Win32.RegistryKey registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            Assembly curAssembly = Assembly.GetExecutingAssembly();

            this.RunOnStartup = (registryKey.GetValue(curAssembly.GetName().Name) != null);

            // After the first ever login, set MinimizeOnClose to true.
            // Future login actions will simply ignore this.
            if (Properties.Settings.Default.IsFirstLogin)
            {
                Properties.Settings.Default.IsFirstLogin = false;
                this.MinimizeOnClose = true;
            }

            this.ActivateItem(this.UserVM);

            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);
        }

        #endregion
    }
}
