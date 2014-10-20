using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeApp
{
    public static class Utilities
    {
        /// <summary>
        /// Returns the application's installation directory.
        /// </summary>
        /// <returns>DirectoryInfo</returns>
        public static DirectoryInfo GetInstallDirectoryInfo()
        {
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            return new FileInfo(assemblyLocation).Directory;
        }
    }
}
