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
using System.Windows;
using Custom.Windows;

namespace DeepfreezeApp
{
    [Export(typeof(IAboutViewModel))]
    public class AboutViewModel : Conductor<Screen>.Collection.AllActive, IAboutViewModel, IHandle<ICheckForUpdateMessage>
    {
        #region members

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(AboutViewModel));
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private bool _isOpen;
        private bool _isBusy = false;
        private bool _isUpToDate = true;
        private string _errorMessage;
        private string _updateMessage = Properties.Resources.UpToDateText;
        private bool _restartNeeded = false;

        #endregion

        #region constructors
        public AboutViewModel() { }

        [ImportingConstructor]
        public AboutViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
        }

        #endregion

        #region properties

        public bool IsOpen
        {
            get { return this._isOpen; }
            set { this._isOpen = value; NotifyOfPropertyChange(() => IsOpen); }
        }

        public bool IsBusy
        {
            get { return this._isBusy; }
            set 
            { 
                this._isBusy = value; 
                NotifyOfPropertyChange(() => IsBusy);
                NotifyOfPropertyChange(() => ShowCheckForUpdate); 
            }
        }

        public bool IsUpToDate
        {
            get { return this._isUpToDate; }
            set { this._isUpToDate = value; NotifyOfPropertyChange(() => IsUpToDate); }
        }

        public bool RestartNeeded
        {
            get { return this._restartNeeded; }
            set 
            { 
                this._restartNeeded = value; 
                NotifyOfPropertyChange(() => RestartNeeded);
                NotifyOfPropertyChange(() => ShowCheckForUpdate);
            }
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

        public string UpdateMessage
        {
            get { return this._updateMessage; }
            set { this._updateMessage = value; NotifyOfPropertyChange(() => this.UpdateMessage); }
        }

        public string RestartNeededText
        {
            get { return Properties.Resources.RestartNeededText; }
        }

        public string CheckForUpdateText
        { get { return Properties.Resources.CheckForUpdateText; } }

        public string CheckForUpdateTooltip
        { get { return Properties.Resources.CheckForUpdateTooltip; } }

        public bool ShowCheckForUpdate
        { get { return !(IsBusy || this.RestartNeeded); } }

        public string CheckForUpdateAutomaticText
        { get { return Properties.Resources.CheckForUpdateAutomaticText; } }

        public bool DoAutomaticUpdates
        {
            get { return Properties.Settings.Default.DoAutomaticUpdates; }
            set
            {
                Properties.Settings.Default.DoAutomaticUpdates = value;
                Properties.Settings.Default.Save();
                NotifyOfPropertyChange(() => this.DoAutomaticUpdates);
                NotifyOfPropertyChange(() => ShowCheckForUpdate);
            }
        }

        #endregion

        #region methods

        public void ExportLog()
        {
            try
            {
                string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                string newLogPath = Path.Combine(desktopPath, "BigStash" + Properties.Settings.Default.LogFileName);
                File.Copy(Properties.Settings.Default.LogFilePath, newLogPath, true);

                Process.Start(newLogPath);
            }
            catch (Exception e)
            {
                this.ErrorMessage = e.Message;
            }
        }

        public void OpenDeepfreezePage()
        {
            var authority = new Uri(Properties.Settings.Default.BigStashURL).Authority;
            Process.Start(authority);
        }

        public async void CheckForUpdate()
        {
            UpdateCheckInfo info = null;
            this.ErrorMessage = null;

            if (!this._deepfreezeClient.IsInternetConnected)
            {
                this.ErrorMessage = Properties.Resources.ErrorCheckingForUpdateWithoutInternetGenericText;
                return;
            }

            if (ApplicationDeployment.IsNetworkDeployed)
            {
                ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;

                try
                {
                    IsUpToDate = false;
                    this.IsBusy = true;

                    this.UpdateMessage = "Checking for update...";

                    info = ad.CheckForDetailedUpdate(false);

                    // insert a delay here so the user has a chance to actually see the messages
                    // and know that the check is ongoing.
                    await Task.Delay(1000);
                }
                catch (DeploymentDownloadException dde)
                {
                    IsBusy = false;
                    this.ErrorMessage = Properties.Resources.ErrorDownloadingUpdateGenericText;
                    _log.Error("CheckForUpdate threw " + dde.GetType().ToString() + " with message \"" + dde.Message + "\".");
                    return;
                }
                catch (InvalidDeploymentException ide)
                {
                    IsBusy = false;
                    this.ErrorMessage = Properties.Resources.ErrorInvalidDeploymentExceptionGenericText;
                    _log.Error("CheckForUpdate threw " + ide.GetType().ToString() + " with message \"" + ide.Message + "\".");
                    return;
                }
                catch (InvalidOperationException ioe)
                {
                    IsBusy = false;
                    this.ErrorMessage = Properties.Resources.ErrorInvalidOperationCheckingForUpdateGenericText;
                    _log.Error("CheckForUpdate threw " + ioe.GetType().ToString() + " with message \"" + ioe.Message + "\".");
                    return;
                }

                if (info.UpdateAvailable)
                {
                    try
                    {
                        this.UpdateMessage = "Updating to latest version...";

                        // insert a delay here so the user has a chance to actually see the messages
                        // and know that the update is installing.
                        await Task.Delay(1000);

                        ad.UpdateAsync();

                        this.RestartNeeded = true;
                        this.IsBusy = false;
                        this.UpdateMessage = "Update completed successfully. Click above to restart.";

                        Properties.Settings.Default.RestartAfterUpdate = true;
                        Properties.Settings.Default.Save();
                    }
                    catch (DeploymentDownloadException dde)
                    {
                        IsBusy = false;
                        this.ErrorMessage = Properties.Resources.ErrorDownloadingUpdateGenericText;
                        _log.Error("CheckForUpdate threw " + dde.GetType().ToString() + " with message \"" + dde.Message + "\".");
                    }
                }
                else
                {
                    IsBusy = false;
                    IsUpToDate = true;
                    RestartNeeded = false;
                    this.UpdateMessage = Properties.Resources.UpToDateText;
                }
            }
        }

        public void RestartApplication()
        {
            // send a message to pause here before actually starting a new instance
            System.Windows.Forms.Application.Restart();
            Application.Current.Shutdown();
        }

        #endregion

        #region events

        protected override void OnActivate()
        {
            base.OnActivate();

            this._eventAggregator.Subscribe(this);
        }

        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);
        }

        #endregion

        #region message_handlers

        public void Handle(ICheckForUpdateMessage message)
        {
            if (message != null && !RestartNeeded)
            {
                this.CheckForUpdate();
            }
        }

        #endregion
    }
}
