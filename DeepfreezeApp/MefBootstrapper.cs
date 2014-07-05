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

namespace DeepfreezeApp
{
    public class MefBootstrapper : BootstrapperBase
    {
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
            // Set Application local app data folder and file paths
            // in Application.Properties for use in this application instance.
            SetApplicationPathsProperties();

            // if LOCALAPPDATA\Deepfreeze doesn't exist, create it.
            CreateLocalApplicationDataDirectory();

            // Read local settings.json and set DeepfreezeClient settings.
            SetDeepfreezeClientSettings();

            DisplayRootViewFor<IShell>();
        }

        private void CreateLocalApplicationDataDirectory()
        {
            // if LOCALAPPDATA\Deepfreeze doesn't exist, create it.
            try
            {
                if (!Directory.Exists(Properties.Settings.Default.LocalAppDataDFFolder))
                    Directory.CreateDirectory(Properties.Settings.Default.LocalAppDataDFFolder);
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

            // %APPDATA\Deepfreeze\preferences.json
            Properties.Settings.Default.SettingsFilePath =
                    Path.Combine(
                        Properties.Settings.Default.LocalAppDataDFFolder,
                        Properties.Settings.Default.SettingsFileName
                    );
        }

        private void SetDeepfreezeClientSettings()
        {
            try
            {
                var client = IoC.Get<IDeepfreezeClient>();

                // try to read %APPDATA\Deepfreeze\settings.json
                // to instatiate client settings for last user and token.
                var content = File.ReadAllText(Properties.Settings.Default.SettingsFilePath, Encoding.ASCII);

                if (content != null)
                    client.Settings = JsonConvert.DeserializeObject<Settings>(content);

            }
            catch (FileNotFoundException e) { } // do nothing, client has null settings.
            catch (JsonReaderException e) { } // do nothing, client has null settings.
        }
    }
}
