using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

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

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger((System.Reflection.MethodBase.GetCurrentMethod().DeclaringType));

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
            set 
            { 
                Properties.Settings.Default.MinimizeOnClose = value; 
                Properties.Settings.Default.Save(); 
                NotifyOfPropertyChange(() => this.MinimizeOnClose); 
            }
        }

        public bool VerboseDebugLogging
        {
            get { return Properties.Settings.Default.VerboseDebugLogging; }
            set
            {
                Properties.Settings.Default.VerboseDebugLogging = value;
                Properties.Settings.Default.Save(); 
                NotifyOfPropertyChange(() => this.VerboseDebugLogging);
                FlipVerboseDebugLogging();
            }
        }
        #endregion

        #region methods

        public void RunOnStartupChanged()
        {
            Microsoft.Win32.RegistryKey registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            Assembly curAssembly = Assembly.GetExecutingAssembly();

            if (this.RunOnStartup)
            {
                registryKey.SetValue(curAssembly.GetName().Name, curAssembly.Location + " -m");
            }
            else
            {
                registryKey.DeleteValue(curAssembly.GetName().Name, false);
            }
        }

        #endregion

        #region private_methods

        private void FlipVerboseDebugLogging()
        {
            string debugMode = String.Empty;

            if (this.VerboseDebugLogging)
            {
                ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root.Level = log4net.Core.Level.Debug;
                debugMode = log4net.Core.Level.Debug.DisplayName;
            }
            else
            {
                ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root.Level = log4net.Core.Level.Info;
                debugMode = log4net.Core.Level.Info.DisplayName;
            }

            ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).RaiseConfigurationChanged(EventArgs.Empty);

            _log.Warn("Changed minimum logging level to " + debugMode + ".");
        }

        #endregion

        #region events

        protected override void OnActivate()
        {
            Microsoft.Win32.RegistryKey registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            Assembly curAssembly = Assembly.GetExecutingAssembly();

            var key = (string)registryKey.GetValue(curAssembly.GetName().Name);
            this.RunOnStartup = (key != null);

            // Check if the registry key points to the current assembly's location.
            // We need to do this in order to update the key in cases it's an updated version,
            // so the key needs to be updated.
            if (this.RunOnStartup &&
                !key.Contains(curAssembly.Location))
            {
                registryKey.SetValue(curAssembly.GetName().Name, curAssembly.Location + " -m");
            }

            if (Properties.Settings.Default.VerboseDebugLogging)
            {
                this.FlipVerboseDebugLogging();
            }
            
            // After the first ever login, set MinimizeOnClose to true.
            // Future login actions will simply ignore this.
            //if (Properties.Settings.Default.IsFirstLogin)
            //{
            //    Properties.Settings.Default.IsFirstLogin = false;
            //    this.MinimizeOnClose = true;
            //}

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
