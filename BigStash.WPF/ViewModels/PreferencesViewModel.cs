using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using Caliburn.Micro;
using BigStash.SDK;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace BigStash.WPF
{
    [Export(typeof(IPreferencesViewModel))]
    public class PreferencesViewModel : Conductor<Screen>.Collection.AllActive, IPreferencesViewModel
    {
        #region members

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger((System.Reflection.MethodBase.GetCurrentMethod().DeclaringType));

        private readonly IEventAggregator _eventAggregator;
        private readonly IBigStashClient _deepfreezeClient;

        private IUserViewModel _userVM = IoC.Get<IUserViewModel>();

        private bool _isOpen;
        private string _errorMessage;

        #endregion

        #region constructors
        public PreferencesViewModel() { }

        public PreferencesViewModel(IEventAggregator eventAggregator, IBigStashClient deepfreezeClient)
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
            get { return Properties.Settings.Default.RunOnStartup; }
            set
            {
                Properties.Settings.Default.RunOnStartup = value;
                Properties.Settings.Default.Save();
                NotifyOfPropertyChange(() => this.RunOnStartup);
                this.FlipRunOnStartup();
            }
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
                this.FlipVerboseDebugLogging();
            }
        }
        #endregion

        #region private_methods

        private void FlipRunOnStartup()
        {
            using(var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                var curAssembly = Assembly.GetExecutingAssembly();
                var installDirName = SquirrelHelper.GetRootAppDirectoryName();

                if (this.RunOnStartup)
                {
                    registryKey.SetValue(installDirName, curAssembly.Location + " -m");
                }
                else
                {
                    registryKey.DeleteValue(installDirName, false);
                }
            }
        }

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

        private void TryUpdateRunOnStartupKey()
        {
            try
            {
                using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    Assembly curAssembly = Assembly.GetExecutingAssembly();

                    var runOnStartupValue = (string)registryKey.GetValue(curAssembly.GetName().Name);

                    if (String.IsNullOrEmpty(runOnStartupValue))
                    {
                        return;
                    }

                    // It seems the legacy DeepfreezeApp variable name under the Run key exists.
                    // Remove it and write a new one.

                    registryKey.DeleteValue(curAssembly.GetName().Name);

                    this.RunOnStartup = true;
                }
            }
            catch(Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " error, thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);

                this.RunOnStartup = false;
                this.FlipRunOnStartup();
            }
        }

        #endregion

        #region events

        protected override void OnActivate()
        {
            this.TryUpdateRunOnStartupKey();

            if (Properties.Settings.Default.VerboseDebugLogging &&
                ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root.Level == log4net.Core.Level.Info)
            {
                this.FlipVerboseDebugLogging();
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
