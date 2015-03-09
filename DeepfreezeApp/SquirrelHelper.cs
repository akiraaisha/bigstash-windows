using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Squirrel;
using log4net;
using ClickOnceToSquirrelMigrator;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Configuration;

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
            return Properties.Settings.Default.ApplicationName + "Windows";
#endif
        }

        public static string GetUpdateLocation()
        {
#if DEBUG
            return "https://www.bigstash.co/apps/windows/beta/"; // return @"C:\Squirrel\Debug\";
#else
            return Properties.Settings.Default.BigStashUpdateURL;
#endif
        }

        public static Version GetCurrentlyInstalledVersion()
        {
            bool isDebug = false;
            var updateUrl = GetUpdateLocation();

#if DEBUG
            isDebug = true;
            //updateURL = Properties.Settings.Default.LocalUpdateURL;
#endif

            if (isDebug)
            {
                return new Version(0, 0, 0, 0);
            }

            using (var mgr = new Squirrel.UpdateManager(updateUrl, Properties.Settings.Default.ApplicationName, Squirrel.FrameworkVersion.Net45))
            {
                var version = mgr.CurrentlyInstalledVersion();
                return version;
            }

        }

        public static string GetCurrentlyInstalledVersionString()
        {
            bool isDebug = false;

#if DEBUG
            isDebug = true;
#endif

            if (isDebug)
            {
                return DEBUGVERSION;
            }

            var versionString = GetCurrentlyInstalledVersion().ToString();

            return versionString;
        }

        #region update_methods

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
                catch (Exception)
                {
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

        public static async Task<ReleaseEntry> SilentUpdate()
        {
            var appName = GetAppName();
            var updateLocation = GetUpdateLocation();

            using (var mgr = new Squirrel.UpdateManager(updateLocation, appName, Squirrel.FrameworkVersion.Net45))
            {
                return await mgr.UpdateApp();
            }
        }

        #endregion

        /// <summary>
        /// Restarts the app using Squirrel' UpdateManager.RestartApp().
        /// </summary>
        public static void RunUpdatedExe()
        {
            _log.Debug("Called " + Utilities.GetCallerName() + ".");

            // Use Squirrel's restart method.
            // UpdateManager.RestartApp();
            var exeToStart = Path.GetFileName(Assembly.GetEntryAssembly().Location);

            Process.Start(GetUpdateExe(), String.Format("--processStart {0}", exeToStart));
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
                    onInitialInstall: v =>
                    {
                        _log.Warn(Utilities.GetCallerName() + ": Installation in progress.");

                        CreateOrUpdateCustomRegistryEntries(mgr.RootAppDirectory);
                        RegisterShellExtension(mgr.RootAppDirectory);
                        mgr.CreateShortcutForThisExe();
                    },
                    onAppUpdate: v =>
                    {
                        _log.Warn(Utilities.GetCallerName() + ": Update in progress.");

                        mgr.CreateShortcutForThisExe();
                        CreateOrUpdateCustomRegistryEntries(mgr.RootAppDirectory, v.ToString());
                        RegisterShellExtension(mgr.RootAppDirectory);
                    },
                    onAppUninstall: v =>
                    {
                        _log.Warn(Utilities.GetCallerName() + ": Uninstall in progress.");

                        UnregisterShellExtension(mgr.RootAppDirectory);
                        RemoveCustomRegistryEntries(mgr.RootAppDirectory);
                        mgr.RemoveShortcutForThisExe();
                        StopBigStashOnUninstall();
                        CallBatchDelete(mgr.RootAppDirectory);
                    });
            }
        }

        /// <summary>
        /// Remove the immediate ClickOnce ancestor app from the very first Squirrel descendant app.
        /// </summary>
        /// <returns></returns>
        public static async Task TryRemoveClickOnceAncestor()
        {
            try
            {
                var migrator = new InSquirrelAppMigrator(Properties.Settings.Default.ApplicationFullName);
                await migrator.Execute();
            }
            catch (Exception e)
            {
                if (e is InvalidOperationException)
                {
                    var ioe = (InvalidOperationException)e;

                    if (ioe.Message == "Sequence contains no matching element")
                    {
                        // no clickonce installation found to remove, simply return.
                        return;
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Find the running BigStash instance when uninstalling and kill it.
        /// </summary>
        public static void StopBigStashOnUninstall()
        {
            // Notice:
            // When uninstalling, a new app instance tries to uninstall.
            // Make sure to not kill that instance but only the main bigstash instance running,
            // so the uninstall completes successfully.

            Assembly curAssembly = Assembly.GetExecutingAssembly();
            var p = Process.GetProcessesByName(curAssembly.GetName().Name);
            var currentProcess = Process.GetCurrentProcess();

            if (p != null && p.Count() > 0)
            {
                for (int i = 0; i < p.Count(); i++)
                {
                    if (p[i].Id != currentProcess.Id)
                    {
                        p[i].Kill();
                    }
                }
            }
        }

        /// <summary>
        /// Call a batch file to delete the root app directory after the uninstall exits.
        /// </summary>
        /// <param name="rootAppDirectory"></param>
        public static void CallBatchDelete(string rootAppDirectory)
        {
            var pid = Process.GetCurrentProcess().Id;
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("setlocal");
            sb.AppendLine("set /a retry=3");
            sb.AppendLine(":loop");
            sb.AppendLine("if %retry% ==0 (goto :loopexited) else (");
            sb.AppendLine("    tasklist | find \"" + pid + "\" >nul");
            sb.AppendLine("    if not errorlevel 1 (");
            sb.AppendLine("        timeout /t 2 >nul");
            sb.AppendLine("        set /a retry=%retry%-1");
            sb.AppendLine("        goto :loop");
            sb.AppendLine("    )");
            sb.AppendLine(")");
            sb.AppendLine(":loopexited");
            //#if DEBUG
            //            sb.AppendLine("pause");
            //#endif
            sb.AppendLine("rmdir /s /q " + rootAppDirectory);
            //#if DEBUG
            //            sb.AppendLine("pause");
            //#endif
            sb.AppendLine("call :deleteSelf&exit /b");
            sb.AppendLine(":deleteSelf");
            sb.AppendLine("start /b \"\" cmd /c del \"%~f0\"&exit /b");

            var tempPath = Path.GetTempPath();
            var tempSavePath = Path.Combine(tempPath, "bigstash_squirrel_cleaner.bat");

            File.WriteAllText(tempSavePath, sb.ToString(), Encoding.ASCII);

            var p = new Process();
            p.StartInfo.WorkingDirectory = tempPath;
            p.StartInfo.FileName = tempSavePath;

            //#if DEBUG
            //            p.StartInfo.CreateNoWindow = false;
            //            p.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            //#else
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            //#endif

            p.Start();
        }

        /// <summary>
        /// Copy user.config to migrate to after updating.
        /// </summary>
        public static string CopyMigrationUserConfig()
        {
            _log.Debug("Copying user.config for post-update settings migration.");

            // Perform a Save just to be sure that we have the latest valid settings.
            Properties.Settings.Default.Save();

            // Get current user.config.
            var userConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);

            try
            {
                if (userConfig.HasFile)
                {
                    var migrationUserConfigPath = Path.Combine(Properties.Settings.Default.ApplicationDataFolder, "migrate.user.config");

                    if (File.Exists(migrationUserConfigPath))
                    {
                        File.Delete(migrationUserConfigPath);
                    }

                    userConfig.SaveAs(migrationUserConfigPath, ConfigurationSaveMode.Full, true);

                    if (File.Exists(migrationUserConfigPath))
                    {
                        _log.Debug("Created migrate.user.config with path = \"" + migrationUserConfigPath + "\".");
                    }

                    return migrationUserConfigPath;
                }
                else
                {
                    _log.Debug("No settings migration needed since default settings are in use.");

                    return null;
                }
            }
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " error while copying user.config with FilePath = \"" + userConfig.FilePath + "\", thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);
                throw;
            }
        }

        /// <summary>
        /// Get the name of the root app directory using the Squirrel manager.
        /// </summary>
        /// <returns></returns>
        public static string GetRootAppDirectoryName()
        {
            return new DirectoryInfo(GetRootAppDirectory()).Name;
        }

        #region private_methods

        private static string GetUpdateExe()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var updateDotExe = Path.Combine(Path.GetDirectoryName(assembly.Location), "..\\Update.exe");
            var target = new FileInfo(updateDotExe);

            if (!target.Exists) throw new Exception("Update.exe not found, not a Squirrel-installed app?");
            return target.FullName;
        }

        /// <summary>
        /// Create custom registry entries to support app functionality and
        /// we need them to be ready upon installing. These include:
        /// Shell Extensions
        /// </summary>
        private static void CreateOrUpdateCustomRegistryEntries(string rootAppDirectory, string newVersion = null)
        {
            _log.Warn(Utilities.GetCallerName() + " entered.");

            // Get the name of the install path
            var installDirName = new DirectoryInfo(rootAppDirectory).Name;

            // get the path of the most recent version installed
            var latestVerionPath = Directory.GetFiles(rootAppDirectory, "DeepfreezeApp.exe", SearchOption.AllDirectories)
                .OrderByDescending(x => x)
                .FirstOrDefault();

            // open HKCU\Software
            using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software", true))
            {
                // Create or Open HKCU\SOFTWARE\BigStash
                using (var bigstashKey = registryKey.CreateSubKey("BigStash"))
                {
                    // Create or Open HKCU\SOFTWARE\BigStash\<installDirName>
                    using (var installKey = bigstashKey.CreateSubKey(installDirName))
                    {
                        // Set name/value pair to hold latest version executable's path.
                        installKey.SetValue("LatestVersionPath", latestVerionPath);
                    }
                }
            }

            // Open HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall
            using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall", true))
            {
                // Create or Open HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\<installDirName>
                using (var bigstashUninstallKey = registryKey.CreateSubKey(installDirName))
                {
                    // Set the DisplayIcon to show the icon in Programs and Features.
                    bigstashUninstallKey.SetValue("DisplayIcon", latestVerionPath);

                    if (!String.IsNullOrEmpty(newVersion))
                    {
                        // Update the display version.
                        bigstashUninstallKey.SetValue("DisplayVersion", newVersion);
                    }
                }
            }

            // Open HKCU\Software\Microsoft\Windows\CurrentVersion\Run
            using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                var curAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
                var startupValue = (string)registryKey.GetValue(curAssemblyName);

                // If the name DeepfreezeApp exists then delete that key
                // since its how it was used in older versions (<= 1.4.1.5)
                // and create a new one with root app directory as its name
                // and latestVersionPath as its value.
                if (!String.IsNullOrEmpty(startupValue))
                {
                    // delete the old value
                    registryKey.DeleteValue(curAssemblyName, false);

                    // Also, update the relevant app setting.
                    Properties.Settings.Default.RunOnStartup = true;
                }
                else
                {
                    Properties.Settings.Default.RunOnStartup = false;
                }

                Properties.Settings.Default.Save();

                // What happens if the above has already been done in a previous update?
                // Then we need to just check the app setting and if it's true
                // then update the value with latestVersionPath.

                if (Properties.Settings.Default.RunOnStartup)
                {
                    registryKey.SetValue(installDirName, latestVerionPath + " -m");
                }
            }
        }

        /// <summary>
        /// Remove custom registry entries created to support app functionality
        /// and should not be left behind when uninstalling. These include:
        /// Run at startup
        /// Shell Extensions
        /// </summary>
        private static void RemoveCustomRegistryEntries(string rootAppDirectory)
        {
            _log.Warn(Utilities.GetCallerName() + " entered.");

            // Get the name of the install path
            var installDirName = new DirectoryInfo(rootAppDirectory).Name;

            // Remove run at startup entry
            using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                registryKey.DeleteValue(installDirName, false);

                // Also try removing the legacy key.
                var curAssemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                registryKey.DeleteValue(curAssemblyName, false);
            }

            // Open HKCU\Software.
            using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE", true))
            {
                bool delBigStashKey = false;

                // Open HKCU\Software\BigStash.
                using (var bigstashKey = registryKey.OpenSubKey("BigStash", true))
                {
                    // Remove Software\BigStash\<installDirName> key.
                    bigstashKey.DeleteSubKey(installDirName, false);

                    // If HKCU\Software\BigStash doesn't have any sub keys
                    // (that is no BigStash app, stable or beta, is installed
                    // then delete it.
                    if (bigstashKey.SubKeyCount == 0)
                    {
                        delBigStashKey = true;
                    }
                }

                if (delBigStashKey)
                {
                    registryKey.DeleteSubKey("BigStash", false);
                }
            }
        }

        private static void RegisterShellExtension(string rootAppDirectory)
        {
            _log.Warn(Utilities.GetCallerName() + " entered.");

            string dllName = Directory.GetFiles(rootAppDirectory, "BigStashExt.dll", SearchOption.AllDirectories).FirstOrDefault();

            if (Environment.Is64BitOperatingSystem)
            {
                dllName = Directory.GetFiles(rootAppDirectory, "BigStashExt64.dll", SearchOption.AllDirectories).FirstOrDefault();
            }

            if (String.IsNullOrEmpty(dllName))
            {
                _log.Error(Utilities.GetCallerName() + " error while trying to locate the shell context menu extension dll.");
                return;
            }

            _log.Info(Utilities.GetCallerName() + ": Found dll to unregister at path '" + dllName + "'.");

            var p = Process.Start("regsvr32.exe", "/s " + dllName);

            p.WaitForExit();

            _log.Info(Utilities.GetCallerName() + ": Dll registration exited with code " + p.ExitCode + ".");

            if (p.ExitCode != 0)
            {
                _log.Error(Utilities.GetCallerName() + " error while registering shell extension '" + dllName + "'.");
                return;
            }
        }

        private static void UnregisterShellExtension(string rootAppDirectory)
        {
            _log.Warn(Utilities.GetCallerName() + " entered.");

            string dllName = Directory.GetFiles(rootAppDirectory, "BigStashExt.dll", SearchOption.AllDirectories).FirstOrDefault();

            if (Environment.Is64BitOperatingSystem)
            {
                dllName = Directory.GetFiles(rootAppDirectory, "BigStashExt64.dll", SearchOption.AllDirectories).FirstOrDefault();
            }

            if (String.IsNullOrEmpty(dllName))
            {
                _log.Error(Utilities.GetCallerName() + " error while trying to locate the shell context menu extension dll.");
                return;
            }

            _log.Info(Utilities.GetCallerName() + ": Found dll to unregister at path '" + dllName + "'.");

            var p = Process.Start("regsvr32.exe", "/u /s " + dllName);

            p.WaitForExit();

            _log.Info(Utilities.GetCallerName() + ": Dll unregistration exited with code " + p.ExitCode + ".");

            if (p.ExitCode != 0)
            {
                _log.Error(Utilities.GetCallerName() + " error while unregistering shell extension '" + dllName + "'.");
            }

            try
            {
                // Try to delete the extension dll file.
                File.Delete(dllName);
            }
            catch (UnauthorizedAccessException uae)
            {
                _log.Error(Utilities.GetCallerName() + " error while deleting shell extension dll file '" + dllName + "'.", uae);
            }

            // Either way, try to rename the app version directory because future installations,
            // while in the same windows session, of any app version,
            // will try to delete the directory, failing to do so (if the dll is still in use by explorer.exe)
            if (File.Exists(dllName))
            {
                var dllFile = new FileInfo(dllName);
                try
                {
                    // Try renaming the dir by executing a batch file after the uninstall finishes.
                    // The renamed folder has the original name plus the current datetime in utc and '.old' suffix.
                    CallRenameInUseDirToOld(dllFile.DirectoryName, dllFile.Directory.Name + DateTime.UtcNow.ToString(".yyyy_MM_dd_hh_mm_ss") + ".old");
                    return;
                }
                catch (Exception e)
                {
                    // If we still can't do anything about it, well let it rest.
                    _log.Error(Utilities.GetCallerName() + " error while renaming directory '" + dllFile.Directory.Name + "'.", e);
                    return;
                }
            }
        }

        /// <summary>
        /// Get the root app directory using the Squirrel manager.
        /// </summary>
        /// <returns></returns>
        private static string GetRootAppDirectory()
        {
            var appName = SquirrelHelper.GetAppName();
            var updateLocation = SquirrelHelper.GetUpdateLocation();

            using (var mgr = new UpdateManager(updateLocation, appName, FrameworkVersion.Net45))
            {
                return mgr.RootAppDirectory;
            }
        }

        /// <summary>
        /// Call a batch file to rename the app version directory after the uninstall exits.
        /// </summary>
        /// <param name="rootAppDirectory"></param>
        public static void CallRenameInUseDirToOld(string path, string newName)
        {
            var pid = Process.GetCurrentProcess().Id;
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("setlocal");
            sb.AppendLine("set /a retry=3");
            sb.AppendLine(":loop");
            sb.AppendLine("if %retry% ==0 (goto :loopexited) else (");
            sb.AppendLine("    tasklist | find \"" + pid + "\" >nul");
            sb.AppendLine("    if not errorlevel 1 (");
            sb.AppendLine("        timeout /t 2 >nul");
            sb.AppendLine("        set /a retry=%retry%-1");
            sb.AppendLine("        goto :loop");
            sb.AppendLine("    )");
            sb.AppendLine(")");
            sb.AppendLine(":loopexited");
            sb.Append("ren ");
            sb.Append(path);
            sb.Append(" ");
            sb.AppendLine(newName);
            sb.AppendLine("call :deleteSelf&exit /b");
            sb.AppendLine(":deleteSelf");
            sb.AppendLine("start /b \"\" cmd /c del \"%~f0\"&exit /b");

            var tempPath = Path.GetTempPath();
            var tempSavePath = Path.Combine(tempPath, "bigstash_squirrel_renamer.bat");

            File.WriteAllText(tempSavePath, sb.ToString(), Encoding.ASCII);

            var p = new Process();
            p.StartInfo.WorkingDirectory = tempPath;
            p.StartInfo.FileName = tempSavePath;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            p.Start();
        }

        #endregion
    }
}
