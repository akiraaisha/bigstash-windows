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
            var app = Application as InstanceAwareApplication;
            if (!(app == null || app.IsFirstInstance))
                app.Shutdown();

            // Else go on with normal startup.

            log4net.Config.XmlConfigurator.Configure(new FileInfo("Log4Net.config"));

            Log.Info("Starting up a new instance of Deepfreeze for Windows.");
            Log.Info("******************************************************");
            Log.Info("******************************************************");
            Log.Info("*********                                    *********");
            Log.Info("*********       Deepfreeze for Windows       *********");
            Log.Info("*********                                    *********");
            Log.Info("******************************************************");
            Log.Info("******************************************************");

            // Set Application local app data folder and file paths
            // in Application.Properties for use in this application instance.
            SetApplicationPathsProperties();

            // if LOCALAPPDATA\Deepfreeze doesn't exist, create it.
            CreateLocalApplicationDataDirectory();

            DisplayRootViewFor<IShell>();

            // Catch with args and forward a message with them
            if (e.Args.Length > 0)
            {
                this.CatchAndForwardArgs(e.Args[0]);
            }

            base.OnStartup(sender, e);
        }

        protected override async void OnExit(object sender, EventArgs e)
        {
            Log.Info("Exiting application.");

            var client = IoC.Get<IDeepfreezeClient>();

            if (client.IsLogged())
            {
                Log.Info("Saving preferences.json at \"" + Properties.Settings.Default.SettingsFilePath + "\".\n\n");

                // Save preferences file.
                await Task.Run(() => LocalStorage.WriteJson(Properties.Settings.Default.SettingsFilePath, client.Settings, Encoding.ASCII))
                    .ConfigureAwait(false);
            }
            else
            {
                Log.Info("Deleting preferences.json at \"" + Properties.Settings.Default.SettingsFilePath + "\"\n\n");
                File.Delete(Properties.Settings.Default.SettingsFilePath);
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
            // if LOCALAPPDATA\Deepfreeze doesn't exist, create it.
            try
            {
                if (!Directory.Exists(Properties.Settings.Default.LocalAppDataDFFolder))
                {
                    Log.Info("Creating LocalAppData Deepfreeze directory.");

                    Directory.CreateDirectory(Properties.Settings.Default.LocalAppDataDFFolder);
                }
            }
            catch (Exception ex) { }
        }

        private void SetApplicationPathsProperties()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            
            // %APPDATA\Deepfreeze\
            Properties.Settings.Default.LocalAppDataDFFolder =
                Path.Combine(
                    localAppData,
                    Properties.Settings.Default.ApplicationName
                );

            Log.Info("Setting LocalAppData Deepfreeze directory path as \"" + Properties.Settings.Default.LocalAppDataDFFolder + "\".");

            // %APPDATA\Deepfreeze\preferences.json
            Properties.Settings.Default.SettingsFilePath =
                    Path.Combine(
                        Properties.Settings.Default.LocalAppDataDFFolder,
                        Properties.Settings.Default.SettingsFileName
                    );

            Log.Info("Setting Deepfreeze preferences file path as \"" + Properties.Settings.Default.SettingsFilePath + "\".");

            // %APPDATA\Deepfreeze\uploads\
            Properties.Settings.Default.UploadsFolderPath =
                    Path.Combine(
                        Properties.Settings.Default.LocalAppDataDFFolder,
                        Properties.Settings.Default.UploadsFolderName
                    );

            Log.Info("Setting Deepfreeze local upload files' path as \"" + Properties.Settings.Default.UploadsFolderPath + "\".");

            // %APPDATA\Deepfreeze\endpoint.json
            Properties.Settings.Default.EndpointFilePath =
                    Path.Combine(
                        Properties.Settings.Default.LocalAppDataDFFolder,
                        Properties.Settings.Default.EndpointFileName
                    );

            Log.Info("Setting Deepfreeze endpoint file path as \"" + Properties.Settings.Default.EndpointFilePath + "\".");

            // %APPDATA\Deepfreeze\DFLog.txt
            Properties.Settings.Default.LogFilePath =
                    Path.Combine(
                        Properties.Settings.Default.LocalAppDataDFFolder,
                        Properties.Settings.Default.LogFileName
                    );

            Log.Info("Setting Deepfreeze log file path as \"" + Properties.Settings.Default.LogFilePath + "\".");
        }

        #endregion
    }
}
