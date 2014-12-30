using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Squirrel;

namespace DeepfreezeApp
{
    public static class SquirrelHelper
    {
        private const string DEBUGVERSION = "debug";
        private static string updateURL = Properties.Settings.Default.BigStashUpdateURL;

        public static string GetCurrentlyInstalledVersion()
        {
            string version = String.Empty;

#if DEBUG
            version = "debug";
            updateURL = Properties.Settings.Default.LocalUpdateURL;
#endif

            if (version == DEBUGVERSION)
                return version;

            using (var mgr = new Squirrel.UpdateManager(updateURL, Properties.Settings.Default.ApplicationName, Squirrel.FrameworkVersion.Net45))
            {
                return mgr.CurrentlyInstalledVersion().ToString();
            }
        }

        public static async Task<ReleaseEntry> SilentUpdate()
        {
            using (var mgr = new Squirrel.UpdateManager(updateURL, Properties.Settings.Default.ApplicationName, Squirrel.FrameworkVersion.Net45))
            {
                return await mgr.UpdateApp();
            }
        }

        //public static async Task<ReleaseEntry> CheckForUpdate()
        //{
        //    using (var mgr = new Squirrel.UpdateManager(updateURL, Properties.Settings.Default.ApplicationName, Squirrel.FrameworkVersion.Net45))
        //    {
        //        return await mgr.CheckForUpdate();
        //    }
        //}
    }
}
