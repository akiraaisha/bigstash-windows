using Caliburn.Micro;
using DeepfreezeSDK;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Diagnostics;
using System.Deployment.Application;
 

using Newtonsoft.Json;
using DeepfreezeModel;
using System.Threading.Tasks;
using Custom.Windows;

namespace DeepfreezeApp
{
    public class MefBootstrapper : BootstrapperBase
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger((System.Reflection.MethodBase.GetCurrentMethod().DeclaringType));

        private CompositionContainer container;

        public MefBootstrapper()
        {
            Initialize();
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

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            // check if this is the first instance running
            // or a newer with the first instance already running.
            // if this is the case, the newer instance shuts down.
            var app = Application.Current as InstanceAwareApplication;
            if (!(app == null || app.IsFirstInstance))
                app.Shutdown();
            else
            {
                // Else go on with normal startup.
 
                bool firstDeployedRun = ApplicationDeployment.IsNetworkDeployed &&
                    ApplicationDeployment.CurrentDeployment.IsFirstRun;

                // Change default ClickOnce icon in Programs and Features entry.
                // This should execute only on the first app instance run after
                // a network deployment.
                if (firstDeployedRun)
                {
                    SetAddRemoveProgramsIcon();
                }

                // get the application version to be used in user agent header of api requests.
                SetVersionForUserAgent();

                log4net.Config.XmlConfigurator.Configure(new FileInfo("Log4Net.config"));

                Log.Info("Starting up a new instance of BigStash for Windows.");
                Log.Info("*****************************************************");
                Log.Info("*****************************************************");
                Log.Info("*********                                  **********");
                Log.Info("*********       BigStash for Windows       **********");
                Log.Info("*********                                  **********");
                Log.Info("*****************************************************");
                Log.Info("*****************************************************");

                // Set Application local app data folder and file paths
                // in Application.Properties for use in this application instance.
                SetApplicationPathsProperties();

                // if LOCALAPPDATA\BigStash doesn't exist, create it.
                CreateLocalApplicationDataDirectory();

                // If this version is network deployed, is the first instance run after installing/updating
                // and is equal to the 1st release of Bigstash for Windows (version 1.2.0) then migrate old deepfreeze application data.
                if (ApplicationDeployment.IsNetworkDeployed &&
                    ApplicationDeployment.CurrentDeployment.IsFirstRun &&
                    ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString() == "1.2.0.0")
                {
                    // Migrate deepfreeze data to bigstash app data directory.
                    // This step is needed for all clients updating from any Deepfreeze.io app version
                    // to any BigStash version.
                    MigrateDeepfreezeData();
                }
                
                DisplayRootViewFor<IShell>();

                // Catch with args and forward a message with them
                if (e.Args.Length > 0)
                {
                    this.CatchAndForwardArgs(e.Args[0]);
                }

                base.OnStartup(sender, e);
            }
        }

        protected override void OnExit(object sender, EventArgs e)
        {
            var app = Application as InstanceAwareApplication;
            if ((app != null && app.IsFirstInstance))
            {
                Log.Info("Exiting application.");

                // make sure to save one final time the application wide settings.
                Properties.Settings.Default.Save();

                var client = IoC.Get<IDeepfreezeClient>();

                if (client.IsLogged())
                {
                    Log.Info("Saving preferences.json at \"" + Properties.Settings.Default.SettingsFilePath + "\".\n\n");

                    // Reset the api endpoint to the default 'ServerBaseAddress' before saving the preferences file
                    // for the last time.
                    this.ResetDebugServerBaseAddress(client);

                    // Save preferences file.
                    LocalStorage.WriteJson(Properties.Settings.Default.SettingsFilePath, client.Settings, Encoding.ASCII);
                }
                else
                {
                    Log.Info("Deleting preferences.json at \"" + Properties.Settings.Default.SettingsFilePath + "\"\n\n");
                    File.Delete(Properties.Settings.Default.SettingsFilePath);
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

            Log.Error("Unhandled exception occured, thrown " + e.Exception.GetType().Name + " with message \"" + e.Exception.Message + "\".");

            Application.Shutdown();
        }

        private void OnStartupNextInstance(object sender, StartupNextInstanceEventArgs e)
        {
            // Catch with args and forward a message with them
            if (e.Args.Length > 0)
            {
                this.CatchAndForwardArgs(e.Args[0]);
            }
        }
        #region private_methods

        protected override void PrepareApplication()
        {
            base.PrepareApplication();
            var application = (InstanceAwareApplication)Application;
            application.StartupNextInstance += OnStartupNextInstance;
        }

        private void CatchAndForwardArgs(string arg)
        {
            var eventAggregator = IoC.Get<IEventAggregator>();
            var startUpArgsMessage = IoC.Get<IStartUpArgsMessage>();
            startUpArgsMessage.StartUpArgument = arg;
            eventAggregator.PublishOnUIThread(startUpArgsMessage);
        }

        private void CreateLocalApplicationDataDirectory()
        {
            // if LOCALAPPDATA\Deepfreeze.io doesn't exist, create it.
            try
            {
                if (!Directory.Exists(Properties.Settings.Default.ApplicationDataFolder))
                {
                    Log.Info("Creating LocalAppData BigStash directory.");

                    Directory.CreateDirectory(Properties.Settings.Default.ApplicationDataFolder);
                }
            }
            catch (Exception ex) { }
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

            Log.Info("Setting LocalAppData BigStash directory path as \"" + Properties.Settings.Default.ApplicationDataFolder + "\".");

            // %APPDATA\BigStash\preferences.json
            Properties.Settings.Default.SettingsFilePath =
                    Path.Combine(
                        Properties.Settings.Default.ApplicationDataFolder,
                        Properties.Settings.Default.SettingsFileName
                    );

            Log.Info("Setting BigStash preferences file path as \"" + Properties.Settings.Default.SettingsFilePath + "\".");

            // %APPDATA\BigStash\uploads\
            Properties.Settings.Default.UploadsFolderPath =
                    Path.Combine(
                        Properties.Settings.Default.ApplicationDataFolder,
                        Properties.Settings.Default.UploadsFolderName
                    );

            Log.Info("Setting BigStash local upload files' path as \"" + Properties.Settings.Default.UploadsFolderPath + "\".");

            // %APPDATA\BigStash\endpoint.json
            Properties.Settings.Default.EndpointFilePath =
                    Path.Combine(
                        Properties.Settings.Default.ApplicationDataFolder,
                        Properties.Settings.Default.EndpointFileName
                    );

            Log.Info("Setting BigStash endpoint file path as \"" + Properties.Settings.Default.EndpointFilePath + "\".");

            // %APPDATA\BigStash\DFLog.txt
            Properties.Settings.Default.LogFilePath =
                    Path.Combine(
                        Properties.Settings.Default.ApplicationDataFolder,
                        Properties.Settings.Default.LogFileName
                    );

            Log.Info("Setting BigStash log file path as \"" + Properties.Settings.Default.LogFilePath + "\".");
        }

        /// <summary>
        /// Set the deepfreeze icon in Control Panel's Programs and Features entry 
        /// for the BigStash for Windows application.
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

                    // if subKey points to the correct BigStash for Windows entry
                    // then update its DisplayIcon key value.
                    if (diplayNameValue != null &&
                        diplayNameValue.ToString() == Properties.Settings.Default.ApplicationFullName)
                    {
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
            catch (Exception ex) { }
        }

        [Conditional("DEBUG")]
        private void ResetDebugServerBaseAddress(IDeepfreezeClient client)
        {
            client.Settings.ApiEndpoint = Properties.Settings.Default.ServerBaseAddress;
        }

        private void SetVersionForUserAgent()
        {
            var client = IoC.Get<IDeepfreezeClient>();

            if (ApplicationDeployment.IsNetworkDeployed)
                client.ApplicationVersion = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
            else
                client.ApplicationVersion = "debug";

        }

        private void MigrateDeepfreezeData()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var deepfreezeFolderPath = Path.Combine(localAppData, Properties.Settings.Default.DeepfreezeApplicationFolderName);


            // If Deepfreeze.io local app data folder exists, then copy all it's content's to BigStash folder,
            // except for the old log file.
            if (Directory.Exists(deepfreezeFolderPath))
            {
                Log.Info("Migrating Deepfreeze data to BigStash application data directory.");

                try
                {
                    Utilities.DirectoryCopy(deepfreezeFolderPath, Properties.Settings.Default.ApplicationDataFolder, true);

                    // Delete old DFLog files copied to the BigStash directory since they're now useless.
                    var oldLogs = Directory.GetFiles(Properties.Settings.Default.ApplicationDataFolder, "DFLog*").ToList();
                    foreach(var log in oldLogs)
                    {
                        File.Delete(log);
                    }

                    // Delete the Deepfreeze.io directory.
                    Directory.Delete(deepfreezeFolderPath, true);
                }
                catch(Exception e)
                { }
            }
        }

        #endregion
    }
}
