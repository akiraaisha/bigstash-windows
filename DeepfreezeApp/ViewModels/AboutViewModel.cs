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
using System.Windows.Controls;


namespace BigStash.WPF
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
        private static readonly TimeSpan INITIAL_FAST_CHECK_TIMESPAN = new TimeSpan(0, 1, 0);
        private static readonly TimeSpan DAILY_CHECK_TIMESPAN = new TimeSpan(1, 0, 0, 0);

        private string _licenseText = default(string);
        private string _packageName = default(string);
        private BindableCollection<KeyValuePair<string, string>> _licenses = new BindableCollection<KeyValuePair<string, string>>();
        private int _tabSelectedIndex = 0;
        private int _licensesSelectedIndex = -1;

        // get the .exe location
        private static readonly string _basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        private static readonly string _docsPath = _basePath + "\\docs";
        private static readonly string _licensesPath = _docsPath + "\\licenses";

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

        public string ReleaseNotesText
        {
            get { return this.ReadTextFile(_docsPath + "\\ReleaseNotes.txt"); }
        }

        public string LicenseText
        {
            get { return this._licenseText; }
            set
            {
                this._licenseText = value;
                NotifyOfPropertyChange(() => this.LicenseText);
            }
        }

        public string PackageName
        {
            get { return this._packageName; }
            set
            {
                this._packageName = value;
                NotifyOfPropertyChange(() => this.PackageName);
            }
        }

        public BindableCollection<KeyValuePair<string, string>> Licenses
        {
            get { return this._licenses; }
            set
            {
                this._licenses = value;
                NotifyOfPropertyChange(() => this.Licenses);
            }
        }

        public int LicensesSelectedIndex
        {
            get { return this._licensesSelectedIndex; }
            set
            {
                this._licensesSelectedIndex = value;
                NotifyOfPropertyChange(() => this.LicensesSelectedIndex);
            }
        }

        public int TabSelectedIndex
        {
            get { return this._tabSelectedIndex; }
            set
            {
                this._tabSelectedIndex = value;
                NotifyOfPropertyChange(() => this.TabSelectedIndex);
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

        public async Task CheckForUpdate()
        {
            this.ErrorMessage = null;
            bool isDebug = false;
#if DEBUG
            isDebug = true;
#endif
            // when debugging do nothing.
            if (isDebug)
            {
                return;
            }

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

                // Check if older releases were fetched for installing.
                // This is happens when the running instance has a more recent version
                // than the one reported in the remote RELEASES file.
                // If this is the case, then we remove the older entries from ReleasesToApply
                // and continue with installing only if the list is not empty (it contains newer versions).
                if (hasUpdates)
                {
                    var installedVersion = SquirrelHelper.GetCurrentlyInstalledVersion();
                    var releasesToRemove = new List<Squirrel.ReleaseEntry>();

                    foreach (var release in updateInfo.ReleasesToApply.ToList())
                    {
                        if (release.Version <= installedVersion)
                        {
                            releasesToRemove.Add(release);
                        }
                    }

                    foreach (var releaseToRemove in releasesToRemove)
                    {
                        updateInfo.ReleasesToApply.Remove(releaseToRemove);
                    }

                    hasUpdates = updateInfo.ReleasesToApply.Count > 0;

                    releasesToRemove.Clear();
                    releasesToRemove = null;
                }

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
                await SquirrelHelper.DownloadReleasesAsync(updateInfo.ReleasesToApply).ConfigureAwait(false);

                // Update donwload finished, continue with install
                this.UpdateMessage = Properties.Resources.InstallingUpdateText;
                var applyResult = await SquirrelHelper.ApplyReleasesAsync(updateInfo).ConfigureAwait(false);

                Properties.Settings.Default.SettingsUpgradeRequired = true;
                Properties.Settings.Default.Save();

                // update the UI to show that a restart is needed.
                this.RestartNeeded = true;
                this.UpdateMessage = Properties.Resources.RestartNeededText;

                // send a message using the event aggregator to inform the shellviewmodel that a restart is needed.
                var restartMessage = IoC.Get<IRestartNeededMessage>();
                restartMessage.RestartNeeded = true;
                this._eventAggregator.PublishOnUIThread(restartMessage);
            }
            catch (Exception e)
            {
                // if RELEASES is not found, don't display an error, just write it in the log and show that the app is up to date.
                if (e.Message.Contains("RELEASES") && e.Message.Contains("does not exist"))
                {
                    _log.Warn(Utilities.GetCallerName() + " error, thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\".");

                    this.UpdateMessage = Properties.Resources.NoUpdatesFoundText;
                }
                else
                {
                    _log.Error(Utilities.GetCallerName() + " error, thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);

                    this.UpdateMessage = null;
                    this.ErrorMessage = Properties.Resources.ErrorCheckingForUpdateGenericText;
                }
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        /// <summary>
        /// Restarts the app.
        /// </summary>
        public void RestartApplicationAfterUpdate()
        {
            // Call Update.exe to run the new executable.
            // It will wait until this instance is closed before it executes the new instance.
            SquirrelHelper.RunUpdatedExe();

            Properties.Settings.Default.RestartAfterUpdate = true;
            Properties.Settings.Default.Save();

            _log.Debug("Sending a graceful restart message.");
            // Let's send a RestartAppMessage with DoGracefulRestart = true;
            var restartAppMessage = IoC.Get<IRestartAppMessage>();
            restartAppMessage.DoGracefulRestart = true;
            restartAppMessage.ConfigureSettingsMigration = true;
            this._eventAggregator.BeginPublishOnUIThread(restartAppMessage);
        }

        #endregion

        #region private_methods

        /// <summary>
        /// Reads a text file found at the specified path parameter and returns its text as a string.
        /// </summary>
        private string ReadTextFile(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " error, thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);

                if (e is FileNotFoundException)
                {
                    return Properties.Resources.FileDoesNotExistText;
                }
                else
                {
                    return Properties.Resources.ErrorReadingTextFileGenericText;
                }
            }
        }

        private async Task LoadLicensesAsync()
        {
            IDictionary<string, string> licenses = new Dictionary<string, string>();
            IList<Task> licenseTasks = new List<Task>();

            // keep a list of known licenses used by the game and filter
            // the local text files, thus ignoring other files that may
            // exist inside the docs\licenses folder.
            string[] knownLicenses = { "AWS SDK for .NET.txt",
                                       "Caliburn.Micro.txt",
                                       "ClickOnce to Squirrel Migrator.txt",
                                       "HardCodet WPF NotifyIcon.txt",
                                       "JSON.NET.txt",
                                       "log4net.txt",
                                       "MahApps.Metro.txt",
                                       "Squirrel.txt",
                                       "WPF Instance Aware Application.txt"
                                     };

            foreach (var path in Directory.GetFiles(_licensesPath))
            {
                // if the file is not a known license, skip it.
                if (!knownLicenses.Contains(Path.GetFileName(path)))
                {
                    continue;
                }

                var task = Task.Run(() => 
                {
                    licenses.Add(new KeyValuePair<string, string>(Path.GetFileNameWithoutExtension(path), this.ReadTextFile(path)));
                });

                licenseTasks.Add(task);
            }

            await Task.WhenAll(licenseTasks);

            this.Licenses.AddRange(licenses.OrderBy(x => x.Key));
            this.LicensesSelectedIndex = 0;
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

        protected override async void OnActivate()
        {
            base.OnActivate();

            this._eventAggregator.Subscribe(this);

            try
            {
                await this.LoadLicensesAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");
            }
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
