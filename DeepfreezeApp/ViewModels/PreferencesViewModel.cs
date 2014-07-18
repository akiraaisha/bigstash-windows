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

        #endregion

        #region methods

        public void ExportLog()
        {
            try
            {
                string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                string newLogPath = Path.Combine(desktopPath, Properties.Settings.Default.LogFileName);
                File.Copy(Properties.Settings.Default.LogFilePath, newLogPath, true);

                Process.Start(newLogPath);
            }
            catch (Exception e)
            {
                this.ErrorMessage = e.Message;
            }
        }

        #endregion

        #region events

        protected override void OnActivate()
        {
            base.OnActivate();

            this.ActivateItem(this.UserVM);
        }

        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);
        }

        #endregion
    }
}
