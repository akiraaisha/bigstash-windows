using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Squirrel;
using log4net;
using ClickOnceToSquirrelMigrator;

namespace DeepfreezeApp
{
    public static class SquirrelHelper
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(SquirrelHelper));
        private const string DEBUGVERSION = "debug";

        public static string GetAppName()
        {
#if DEBUG
            return "BigStashDebug";
#else
            return Properties.Settings.Default.ApplicationName;
#endif
        }

        public static string GetUpdateLocation()
        {
#if DEBUG
            return @"C:\Squirrel\Debug\";
#else
            return Properties.Settings.Default.BigStashUpdateURL;
#endif
        }

        public static string GetCurrentlyInstalledVersion()
        {
            string version = String.Empty;
            bool isDebug = false;
            var updateUrl = GetUpdateLocation();

#if DEBUG
            isDebug = true;
            //updateURL = Properties.Settings.Default.LocalUpdateURL;
#endif

            using (var mgr = new Squirrel.UpdateManager(updateUrl, Properties.Settings.Default.ApplicationName, Squirrel.FrameworkVersion.Net45))
            {
                if (!isDebug)
                {
                    return mgr.CurrentlyInstalledVersion().ToString();
                }
                else
                {
                    return DEBUGVERSION;
                }
            }
        }

        /// <summary>
        /// Check for updates using Squirrel UpdateManager and return the UpdateInfo result.
        /// </summary>
        /// <returns></returns>
        public static async Task<UpdateInfo> CheckForUpdateAsync()
        {
            var appName = GetAppName();
            var updateLocation = GetUpdateLocation();
            object ret = null;

            _log.Debug("Checking for update. Called " + Utilities.GetCallerName() + " with UpdateLocation = \"" + updateLocation + "\" and AppName = " + appName + "\".");

            using (var mgr = new Squirrel.UpdateManager(updateLocation, appName, Squirrel.FrameworkVersion.Net45))
            {
                try
                {
                    var updateInfo = await mgr.CheckForUpdate().ConfigureAwait(false);
                    ret = updateInfo;
                }
                catch (Exception e)
                {
                    _log.Error(Utilities.GetCallerName() + " error while checking for update with UpdateLocation = \"" + updateLocation + "\" and AppName = " + appName + ", thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);
                    throw;
                }

                return (UpdateInfo)ret;
            }
        }

        /// <summary>
        /// Download releases found by a previous update check using Squirrel UpdateManager and return true when finished.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> DownloadReleasesAsync(IEnumerable<ReleaseEntry> releases)
        {
            var appName = GetAppName();
            var updateLocation = GetUpdateLocation();

            _log.Debug("Download Releases. Called " + Utilities.GetCallerName() + " with UpdateLocation = \"" + updateLocation + "\" and AppName = " + appName + "\".");

            using (var mgr = new Squirrel.UpdateManager(updateLocation, appName, Squirrel.FrameworkVersion.Net45))
            {
                try
                {
                    await mgr.DownloadReleases(releases).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _log.Error(Utilities.GetCallerName() + " error while downloading releases with UpdateLocation = \"" + updateLocation + "\" and AppName = " + appName + ", thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);
                    throw;
                }
            }

            return true;
        }

        /// <summary>
        /// Apply releases found and downloaded by a previous update check using Squirrel UpdateManager and return true when finished.
        /// </summary>
        /// <returns></returns>
        public static async Task<string> ApplyReleasesAsync(UpdateInfo updateInfo)
        {
            var appName = GetAppName();
            var updateLocation = GetUpdateLocation();
            var ret = String.Empty;

            _log.Debug("Apply Releases. Called " + Utilities.GetCallerName() + " with UpdateLocation = \"" + updateLocation + "\" and AppName = " + appName + "\".");

            using (var mgr = new Squirrel.UpdateManager(updateLocation, appName, Squirrel.FrameworkVersion.Net45))
            {
                try
                {
                    var s = await mgr.ApplyReleases(updateInfo).ConfigureAwait(false);
                    ret = s;
                }
                catch (Exception e)
                {
                    _log.Error(Utilities.GetCallerName() + " error while applying releases with UpdateLocation = \"" + updateLocation + "\" and AppName = " + appName + ", thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);
                    throw;
                }
            }

            return ret;
        }

        /// <summary>
        /// Restarts the app using Squirrel' UpdateManager.RestartApp().
        /// </summary>
        public static void RestartApp()
        {
            _log.Debug("Called " + Utilities.GetCallerName() + ".");

            // Use Squirrel's restart method.
            UpdateManager.RestartApp();
        }

        public static async Task<ReleaseEntry> SilentUpdate()
        {
            var appName = GetAppName();
            var updateLocation = GetUpdateLocation();

            using (var mgr = new Squirrel.UpdateManager(updateLocation, appName, Squirrel.FrameworkVersion.Net45))
            {
                return await mgr.UpdateApp();
            }
        }

        /// <summary>
        /// This code has to exist in order for Squirrel to work its magic.
        /// What it does, is hook methods to the install/uninstall events.
        /// Basic functionality includes: Create and remove shortcut upon install/uninstall,
        /// as well as when updating.
        /// </summary>
        public static void CustomSquirrelSetup()
        {
            var appName = SquirrelHelper.GetAppName();
            var updateLocation = SquirrelHelper.GetUpdateLocation();

            using (var mgr = new UpdateManager(updateLocation, appName, FrameworkVersion.Net45))
            {
                SquirrelAwareApp.HandleEvents(
                    onInitialInstall: v => mgr.CreateShortcutForThisExe(),
                    onAppUpdate: v => mgr.CreateShortcutForThisExe(),
                    onAppUninstall: v =>
                        {
                            mgr.RemoveShortcutForThisExe();
                            RemoveCustomRegistryEntries();
                        },
                    onFirstRun: () =>
                        {
                            Properties.Settings.Default.MinimizeOnClose = true;
                            Properties.Settings.Default.VerboseDebugLogging = false;
                            Properties.Settings.Default.DoAutomaticUpdates = true;
                            Properties.Settings.Default.Save();
                        }
                        );
            }
        }

        /// <summary>
        /// Create custom registry entries to support app functionality and
        /// we need them to be ready upon installing. These include:
        /// Shell Extensions
        /// </summary>
        private static void CreateCustomRegistryEntries()
        {
            // TODO
            // Add code to add shell extension registration.
        }

        /// <summary>
        /// Remove custom registry entries created to support app functionality
        /// and should not be left behind when uninstalling. These include:
        /// Run at startup
        /// Shell Extensions
        /// </summary>
        private static void RemoveCustomRegistryEntries()
        {
            // Remove run at startup entry
            Microsoft.Win32.RegistryKey registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            System.Reflection.Assembly curAssembly = System.Reflection.Assembly.GetExecutingAssembly();

            // delete if it exists.
            registryKey.DeleteValue(curAssembly.GetName().Name, false);

            // TODO
            // Add code to remove shell extension registration as well.
        }

        /// <summary>
        /// Remove the immediate ClickOnce ancestor app from the very first Squirrel descendant app.
        /// </summary>
        /// <returns></returns>
        public static async Task TryRemoveClickOnceAncestor()
        {
            var migrator = new InSquirrelAppMigrator(Properties.Settings.Default.ApplicationFullName);
            await migrator.Execute();
        }
    }
}
