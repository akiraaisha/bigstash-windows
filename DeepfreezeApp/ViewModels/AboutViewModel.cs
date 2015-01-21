using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.ComponentModel;
using System.Windows.Threading;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

using Caliburn.Micro;
using DeepfreezeSDK;
using Custom.Windows;


namespace DeepfreezeApp
{
    [Export(typeof(IAboutViewModel))]
    public class AboutViewModel : Conductor<Screen>.Collection.AllActive, IAboutViewModel, IHandle<IInternetConnectivityMessage>
    {
        #region members

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(AboutViewModel));
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private bool _isOpen;
        private bool _isBusy = false;
        private bool _isUpToDate = true;
        private string _errorMessage;
        private string _updateMessage;
        private bool _restartNeeded = false;
        private bool _updateFound = false;

        DispatcherTimer _updateTimer;
        private const TimeSpan INITIAL_FAST_CHECK_TIMESPAN = new TimeSpan(0, 1, 0);
        private const TimeSpan DAILY_CHECK_TIMESPAN = new TimeSpan(1, 0, 0, 0);

        #endregion

        #region constructors
        public AboutViewModel() { }

        [ImportingConstructor]
        public AboutViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;

            // get a new DispatcherTimer on the UI Thread.
            this._updateTimer = new DispatcherTimer(INITIAL_FAST_CHECK_TIMESPAN, DispatcherPriority.Normal, UpdateTick, Application.Current.Dispatcher);
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
                return Properties.Resources.VersionHeaderText + " " + this._deepfreezeClient.ApplicationVersion;
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
        { 
            get 
            {
                if (this.IsInternetConnected)
                    return Properties.Resources.CheckForUpdateEnabledTooltipText;
                else
                    return Properties.Resources.CheckForUpdateDisabledTooltipText;
            } 
        }

        public bool ShowCheckForUpdate
        { get { return !(IsBusy || this.RestartNeeded); } }

        public string CheckForUpdateAutomaticText
        { get { return Properties.Resources.CheckForUpdateAutomaticText; } }

        //public bool DoAutomaticUpdates
        //{
        //    get { return Properties.Settings.Default.DoAutomaticUpdates; }
        //    set
        //    {
        //        Properties.Settings.Default.DoAutomaticUpdates = value;
        //        Properties.Settings.Default.Save();

        //        // When changing the automatic updates setting, set the update timer's operation accordingly.
        //        if (value)
        //        {
        //            this._updateTimer.Start();
        //        }
        //        else
        //        {
        //            this._updateTimer.Stop();
        //        }

        //        NotifyOfPropertyChange(() => this.DoAutomaticUpdates);
        //        //NotifyOfPropertyChange(() => ShowCheckForUpdate);
        //    }
        //}

        public bool IsInternetConnected
        {
            get { return this._deepfreezeClient.IsInternetConnected; }
        }

        public System.Windows.Media.Brush CheckForUpdateForeground
        {
            get
            {
                if (this.IsInternetConnected)
                    return System.Windows.Media.Brushes.Blue;
                else
                    return System.Windows.Media.Brushes.Gray;
            }
        }

        public System.Windows.Input.Cursor CheckForUpdateCursor
        {
            get
            {
                if (this.IsInternetConnected)
                    return System.Windows.Input.Cursors.Hand;
                else
                    return System.Windows.Input.Cursors.No;
            }
        }

        public bool UpdateFound
        {
            get
            {
                return this._updateFound;
            }
            set
            {
                this._updateFound = value;
                NotifyOfPropertyChange(() => this.UpdateFound);
            }
        }

        public string UpdateFoundText
        { get { return Properties.Resources.UpdateFoundText; } }

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

        public async Task CheckForUpdate()
        {
            this.ErrorMessage = null;

            // No need to check for update when already checking and/or installing one.
            // The same holds for when a restart is needed because of a previously installed update,
            // while still running the same application instance.
            if (this.IsBusy || this.RestartNeeded)
            {
                return;
            }

            if (!this._deepfreezeClient.IsInternetConnected)
            {
                this.ErrorMessage = Properties.Resources.ErrorCheckingForUpdateWithoutInternetGenericText;
                return;
            }

            IsUpToDate = false;
            this.IsBusy = true;

            // set UI message to checking for update
            this.UpdateMessage = Properties.Resources.CheckingForUpdateText;

            try
            {
                // Verify that old ClickOnce deployments are removed.
                await SquirrelHelper.TryRemoveClickOnceAncestor();

                // check for update
                var updateInfo = await SquirrelHelper.CheckForUpdateAsync();
                var hasUpdates = updateInfo.ReleasesToApply.Count > 0;

                if (!hasUpdates)
                {
                    // no updates found, update the UI and return
                    this.IsBusy = false;
                    this.IsUpToDate = true;
                    this.RestartNeeded = false;
                    this.UpdateFound = false;
                    this.UpdateMessage = Properties.Resources.UpToDateText;
                    return;
                }

                // Update found, continue with download
                this.UpdateMessage = Properties.Resources.DowloadingUpdateText;
                await SquirrelHelper.DownloadReleasesAsync(updateInfo.ReleasesToApply);

                // Update donwload finished, continue with install
                this.UpdateMessage = Properties.Resources.InstallingUpdateText;
                var applyResult = await SquirrelHelper.ApplyReleasesAsync(updateInfo);

                // update the UI to show that a restart is needed.
                this.RestartNeeded = true;
                this.UpdateMessage = Properties.Resources.RestartNeededText;

                // send a message using the event aggregator to inform the shellviewmodel that a restart is needed.
                var restartMessage = IoC.Get<IRestartAppMessage>();
                restartMessage.RestartNeeded = true;
                this._eventAggregator.PublishOnUIThread(restartMessage);
            }
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " error, thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);

                this.UpdateMessage = null;
                this.ErrorMessage = Properties.Resources.ErrorCheckingForUpdateGenericText;
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        /// <summary>
        /// Restarts the app.
        /// </summary>
        public void RestartApplication()
        {
            SquirrelHelper.RestartApp();
        }

        #endregion

        #region events

        private async void UpdateTick(object sender, object e)
        {
            await this.CheckForUpdate();

            if (this._updateTimer.Interval == INITIAL_FAST_CHECK_TIMESPAN)
            {
                // after the 1st automatic check, reset it's interval to 1 day.
                this._updateTimer.Stop();
                this._updateTimer.Interval = DAILY_CHECK_TIMESPAN;
                this._updateTimer.Start();
            }
        }

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

        public void Handle(IInternetConnectivityMessage message)
        {
            if (message != null)
            {
                NotifyOfPropertyChange(() => this.IsInternetConnected);
                NotifyOfPropertyChange(() => this.CheckForUpdateTooltip);
                NotifyOfPropertyChange(() => this.CheckForUpdateForeground);
                NotifyOfPropertyChange(() => this.CheckForUpdateCursor);
            }
        }

        #endregion
    }
}
