using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Diagnostics;

using Newtonsoft.Json;
using Caliburn.Micro;
using DeepfreezeSDK;
using DeepfreezeModel;
using Custom.Windows;
using System.Configuration;

namespace BigStash.WPF
{
    public class MefBootstrapper : BootstrapperBase
    {
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger((System.Reflection.MethodBase.GetCurrentMethod().DeclaringType));

        private CompositionContainer container;

        public MefBootstrapper()
        {
            Initialize();
            SquirrelHelper.CustomSquirrelSetup();
        }

        protected override void Configure()
        {
            var catalog = new AggregateCatalog(
                    AssemblySource.Instance.Select(x => new AssemblyCatalog(x)).OfType<ComposablePartCatalog>()
                    );

            container = new CompositionContainer(catalog);

            var batch = new CompositionBatch();

            batch.AddExportedValue<IWindowManager>(new WindowManager());
            batch.AddExportedValue<IEventAggregator>(new EventAggregator());
            batch.AddExportedValue<IDeepfreezeClient>(new DeepfreezeClient());
            batch.AddExportedValue(container);
            batch.AddExportedValue(catalog);

            container.Compose(batch);
        }

        protected override object GetInstance(Type serviceType, string key)
        {
            string contract = string.IsNullOrEmpty(key) ? AttributedModelServices.GetContractName(serviceType) : key;
            var exports = container.GetExportedValues<object>(contract);

            if (exports.Any())
                return exports.First();

            throw new Exception(string.Format("Could not locate any instances of contract {0}.", contract));
        }

        protected override IEnumerable<object> GetAllInstances(Type serviceType)
        {
            return container.GetExportedValues<object>(AttributedModelServices.GetContractName(serviceType));
        }

        protected override void BuildUp(object instance)
        {
            container.SatisfyImportsOnce(instance);
        }

        protected override async void OnStartup(object sender, StartupEventArgs e)
        {
            // check if this is the first instance running
            // or a newer with the first instance already running.
            // if this is the case, the newer instance shuts down.
            var app = Application.Current as InstanceAwareApplication;
            if (!(app == null || app.IsFirstInstance))
            {
                app.Shutdown();
            }
            else
            {
                // Else go on with normal startup.

                // Try migrating old settings after an update.
                // migrate.user.config must exist in AppData\BigStash\
                TryMigratingOldUserConfig();

                // Upgrade settings from previous squirrel installation.
                //if (Properties.Settings.Default.SettingsUpgradeRequired)
                //{
                //    Properties.Settings.Default.Upgrade();
                //    Properties.Settings.Default.SettingsUpgradeRequired = false;
                //    Properties.Settings.Default.Save();
                //    Properties.Settings.Default.Reload();
                //}

                // Change default ClickOnce icon in Programs and Features entry,
                // if it's not already set.
                // SetAddRemoveProgramsIcon();
                SquirrelHelper.TryRenameOldNameShortcut();

                log4net.Config.XmlConfigurator.Configure(new FileInfo("Log4Net.config"));

//#if DEBUG
//                ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root.Level = log4net.Core.Level.Debug;
//                ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).RaiseConfigurationChanged(EventArgs.Empty);
//                Properties.Settings.Default.VerboseDebugLogging = true;
//#endif
#if DEBUG
                if (!String.IsNullOrEmpty(Properties.Settings.Default.AWSEndpointDefinition))
                    ConfigurationManager.AppSettings["AWSEndpointDefinition"] = Properties.Settings.Default.AWSEndpointDefinition;
#endif
                var currentVersion = SquirrelHelper.GetCurrentlyInstalledVersionString();
                this.SetVersionForUserAgent(currentVersion);

                _log.Info("Starting up a new instance of " + Properties.Settings.Default.ApplicationFullName + " " + currentVersion + ".");
                _log.Info("*****************************************************");
                _log.Info("*****************************************************");
                _log.Info("*********                                  **********");
                _log.Info("*********             BigStash             **********");
                _log.Info("*********                                  **********");
                _log.Info("*****************************************************");
                _log.Info("*****************************************************");

#if DEBUG
                _log.Debug("DEBUG MODE ON");
#endif

                CheckAndEnableVerboseDebugLogging();

                // Set Application local app data folder and file paths
                // in Application.Properties for use in this application instance.
                SetApplicationPathsProperties();

                // if LOCALAPPDATA\BigStash doesn't exist, create it.
                CreateLocalApplicationDataDirectory();

                // Try migrating data from old deepfreeze folder to BigStash folder.
                bool didBigStashMigration = await TryMigrateDeepfreezeData();

                DisplayRootViewFor<IShell>();

                // after showing the main window, if migration took place then show the bigstash update message.
                if (didBigStashMigration)
                {
                    if (!Properties.Settings.Default.BigStashUpdateMessageShown)
                    {
                        await ShowBigStashUpdateMessage();
                    }
                }
                else
                {
                    // this is a clean install of BigStash, just mark the update message as shown.
                    Properties.Settings.Default.BigStashUpdateMessageShown = true;
                    Properties.Settings.Default.Save();
                }

                // Catch with args and forward a message with them
                if (e.Args.Length > 0)
                {
                    this.CatchAndForwardArgs(e.Args);
                }

                base.OnStartup(sender, e);
            }
        }

        protected override void OnExit(object sender, EventArgs e)
        {
            var app = Application as InstanceAwareApplication;
            if ((app != null && app.IsFirstInstance))
            {
                _log.Info("Exiting application.");

                // make sure to save one final time the application wide settings.
                Properties.Settings.Default.Save();

                var client = IoC.Get<IDeepfreezeClient>();

                if (client.IsLogged())
                {
                    _log.Info("Saving preferences.json at \"" + Properties.Settings.Default.SettingsFilePath + "\".");

                    // Reset the api endpoint to the default 'ServerBaseAddress' before saving the preferences file
                    // for the last time.
                    this.ResetDebugServerBaseAddress(client);

                    // Save preferences file.
                    LocalStorage.WriteJson(Properties.Settings.Default.SettingsFilePath, client.Settings, Encoding.UTF8, true);
                }

                if (Properties.Settings.Default.SettingsUpgradeRequired)
                {
                    SquirrelHelper.CopyMigrationUserConfig();
                }
            }

            base.OnExit(sender, e);
        }

        protected override void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Catch any unhandled exception and shut down the app.
            base.OnUnhandledException(sender, e);
            Execute.OnUIThread(() =>
            {
                System.Windows.MessageBox.Show(e.Exception.Message, "Application Exception", System.Windows.MessageBoxButton.OK);
            });

            _log.Error("Unhandled exception occured, thrown " + e.Exception.GetType().Name + " with message \"" + e.Exception.Message + "\".");

            Application.Shutdown();
        }

        private void OnStartupNextInstance(object sender, StartupNextInstanceEventArgs e)
        {
            // Catch with args and forward a message with them
            if (e.Args.Length > 0)
            {
                this.CatchAndForwardArgs(e.Args);
            }
        }

        #region private_methods

        protected override void PrepareApplication()
        {
            base.PrepareApplication();
            var application = (InstanceAwareApplication)Application;
            application.StartupNextInstance += OnStartupNextInstance;
        }

        private void CatchAndForwardArgs(string[] args)
        {
            var eventAggregator = IoC.Get<IEventAggregator>();
            var startUpArgsMessage = IoC.Get<IStartUpArgsMessage>();
            startUpArgsMessage.StartUpArguments = args;
            eventAggregator.PublishOnUIThread(startUpArgsMessage);
        }

        private void CreateLocalApplicationDataDirectory()
        {
            // if LOCALAPPDATA\Deepfreeze.io doesn't exist, create it.
            try
            {
                if (!Directory.Exists(Properties.Settings.Default.ApplicationDataFolder))
                {
                    _log.Info("Creating LocalAppData BigStash directory.");

                    Directory.CreateDirectory(Properties.Settings.Default.ApplicationDataFolder);
                }
            }
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");
            }
        }

        private void SetApplicationPathsProperties()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // %APPDATA\BigStash\
            Properties.Settings.Default.ApplicationDataFolder =
                Path.Combine(
                    localAppData,
                    Properties.Settings.Default.ApplicationName
                );

            _log.Info("Setting LocalAppData BigStash directory path as \"" + Properties.Settings.Default.ApplicationDataFolder + "\".");

            // %APPDATA\BigStash\preferences.json
            Properties.Settings.Default.SettingsFilePath =
                    Path.Combine(
                        Properties.Settings.Default.ApplicationDataFolder,
                        Properties.Settings.Default.SettingsFileName
                    );

            _log.Info("Setting BigStash preferences file path as \"" + Properties.Settings.Default.SettingsFilePath + "\".");

            // %APPDATA\BigStash\uploads\
            Properties.Settings.Default.UploadsFolderPath =
                    Path.Combine(
                        Properties.Settings.Default.ApplicationDataFolder,
                        Properties.Settings.Default.UploadsFolderName
                    );

            _log.Info("Setting BigStash local upload files' path as \"" + Properties.Settings.Default.UploadsFolderPath + "\".");

            // %APPDATA\BigStash\endpoint.json
            Properties.Settings.Default.EndpointFilePath =
                    Path.Combine(
                        Properties.Settings.Default.ApplicationDataFolder,
                        Properties.Settings.Default.EndpointFileName
                    );

            _log.Info("Setting BigStash endpoint file path as \"" + Properties.Settings.Default.EndpointFilePath + "\".");

            // %APPDATA\BigStash\DFLog.txt
            Properties.Settings.Default.LogFilePath =
                    Path.Combine(
                        Properties.Settings.Default.ApplicationDataFolder,
                        Properties.Settings.Default.LogFileName
                    );

            _log.Info("Setting BigStash log file path as \"" + Properties.Settings.Default.LogFilePath + "\".");
        }

        /// <summary>
        /// Set the deepfreeze icon in Control Panel's Programs and Features entry 
        /// for the BigStash application.
        /// </summary>
        private static void SetAddRemoveProgramsIcon()
        {
            try
            {
                string iconName = "bigstash_windows_icon.ico";
                string installDirPath = Utilities.GetInstallDirectoryInfo().ToString();
                string iconSourcePath = Path.Combine(installDirPath, iconName);

                if (!File.Exists(iconSourcePath))
                    return;

                Microsoft.Win32.RegistryKey uninstallKeyParentFolder = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
                IList<string> uninstallSubKeyNames = uninstallKeyParentFolder.GetSubKeyNames().ToList();

                foreach (string uninstallSubKeyName in uninstallSubKeyNames)
                {
                    Microsoft.Win32.RegistryKey subKey = uninstallKeyParentFolder.OpenSubKey(uninstallSubKeyName, true);
                    object diplayNameValue = subKey.GetValue("DisplayName");

                    // if subKey points to the correct BigStash entry
                    // then update its DisplayIcon key value.
                    if (diplayNameValue != null &&
                        diplayNameValue.ToString() == Properties.Settings.Default.ApplicationFullName)
                    {
                        // If it's already set, then simply return.
                        if (subKey.GetValue("DisplayIcon").ToString() == iconSourcePath)
                            return;

                        subKey.SetValue("DisplayIcon", iconSourcePath);
                        break;
                    }
                    else
                    {
                        // Close all other keys.
                        subKey.Close();
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");
            }
        }

        [Conditional("DEBUG")]
        private void ResetDebugServerBaseAddress(IDeepfreezeClient client)
        {
            client.Settings.ApiEndpoint = Properties.Settings.Default.ServerBaseAddress;
        }

        private void SetVersionForUserAgent(string version)
        {
            var client = IoC.Get<IDeepfreezeClient>();
            client.ApplicationVersion = version;
        }

        private async Task ShowBigStashUpdateMessage()
        {
            StringBuilder updateMessage = new StringBuilder();
            updateMessage.AppendLine("Deepfreeze.io is now BigStash!");
            updateMessage.Append(@"[a href='" + Properties.Settings.Default.BigStashURL + @"']");
            updateMessage.Append("www.bigstash.co");
            updateMessage.Append(@"[/a]");
            updateMessage.AppendLine();
            updateMessage.AppendLine();
            updateMessage.Append("You can still connect by using your Deepfreeze.io account credentials.");

            var windowManager = IoC.Get<IWindowManager>();
            await windowManager.ShowMessageViewModelAsync(updateMessage.ToString(), "Update Information", MessageBoxButton.OK);

            Properties.Settings.Default.BigStashUpdateMessageShown = true;
            Properties.Settings.Default.Save();
        }

        private async Task<bool> TryMigrateDeepfreezeData()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var deepfreezeFolderPath = Path.Combine(localAppData, Properties.Settings.Default.DeepfreezeApplicationFolderName);
            var deepfreezeUploadsPath = Path.Combine(deepfreezeFolderPath, "uploads");

            // If the old Deepfreeze.io uploads folder doesn't exist, check for the Deepfreeze.io folder.
            // If it doesn't exist then this is not a migration. If it exists, simply delete it and this is a migration.
            if (!Directory.Exists(deepfreezeUploadsPath))
            {
                // If the deepfreeze folder exists just delete it.
                if (Directory.Exists(deepfreezeFolderPath))
                {
                    // Just delete the old Deepfreeze.io folder.
                    Directory.Delete(deepfreezeFolderPath, true);

                    // deepfreeze existed so return true.
                    return true;
                }

                return false;
            }

            // If the old Deepfreeze.io uploads folder is empty, then don't migrate..
            if (Directory.GetFiles(deepfreezeUploadsPath).Count() == 0)
            {
                // Just delete the old Deepfreeze.io folder since it apparently exists
                // and this is a migration.
                Directory.Delete(deepfreezeFolderPath, true);
                return true;
            }

            // OK we found that old deepfreeze uploads exist. We will now migrate.

            // Go on with the migration process.

            _log.Info("Migrating Deepfreeze data to BigStash application data directory.");

            try
            {
                // copy Deepfreeze.io uploads contents to BigStash uploads folder.
                Utilities.DirectoryCopy(deepfreezeUploadsPath, Properties.Settings.Default.UploadsFolderPath, true);

                // the old deepfreeze endpoint needs to be replaced with the new bigstash endpoint.

                // get the contents of the upload folder.
                var uploadFiles = Directory.GetFiles(Properties.Settings.Default.UploadsFolderPath).ToList();

                IList<Task> replaceTasks = new List<Task>();

                // for all files, replace the endpoint substring found in line index 1.
                foreach (string uploadFile in uploadFiles)
                {
                    var t = Task.Run(() =>
                    {
                        try
                        {
                            // read all file lines to a string array.
                            var lines = File.ReadAllLines(uploadFile);

                            if (lines.Count() > 0)
                            {
                                // line at index 1 is the line that needs to change.
                                // lines[0] is the 1st line containing the '{' character.
                                lines[1] = lines[1].Replace("deepfreeze.io", "bigstash.co");
                            }

                            // write all lines back to the original file.
                            File.WriteAllLines(uploadFile, lines);
                        }
                        catch (Exception) { throw; }
                    });

                    // add the awaitable task to the replaceTasks list.
                    replaceTasks.Add(t);
                }

                // execute all replace tasks in parallel.
                await Task.WhenAll(replaceTasks);

                // Delete the Deepfreeze.io directory.
                Directory.Delete(deepfreezeFolderPath, true);
            }
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");
            }

            // Delete the Deepfreeze.io directory.
            Directory.Delete(deepfreezeFolderPath, true);

            return true;
        }

        /// <summary>
        /// After the clickonce to squirrel migration we need to copy the old.user.config file
        /// which is the user settings as they were the time the migration took place.
        /// This method tries the migration if the old.user.config file exists in the BigStash appdata folder.
        /// </summary>
        private void TryMigratingOldUserConfig()
        {
            // do this to make sure that the user.config dir and file are created.
            Properties.Settings.Default.SettingsUpgradeRequired = false;
            Properties.Settings.Default.Save();

            // check if the old.user.config exists in the local application data folder of bigstash
            var oldUserConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BigStash", "migrate.user.config");

            if (!File.Exists(oldUserConfig))
            {
                return;
            }

            try
            {
                _log.Info("Migrating settings after clickonce-to-squirrel migration.");

                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);

                var configDir = Path.GetDirectoryName(config.FilePath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                File.Copy(oldUserConfig, config.FilePath, true);
                File.Delete(oldUserConfig);
                Properties.Settings.Default.Reload();

                // update settings upgrade required again to false now.
                Properties.Settings.Default.SettingsUpgradeRequired = false;
                Properties.Settings.Default.Save();
            }
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");
            }
        }

        private void CheckAndEnableVerboseDebugLogging()
        {
            string debugMode = String.Empty;

            if (Properties.Settings.Default.VerboseDebugLogging)
            {
                ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root.Level = log4net.Core.Level.Debug;
                debugMode = log4net.Core.Level.Debug.DisplayName;

                ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).RaiseConfigurationChanged(EventArgs.Empty);

                _log.Warn("Changed minimum logging level to " + debugMode + ".");
            }
        }

        #endregion
    }
}
