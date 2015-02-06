using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

using DeepfreezeModel;
using Newtonsoft.Json;

namespace DeepfreezeApp
{
    public static class Utilities
    {
        private static char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        private static Regex xmlValidCharsRegEx = new Regex(@"[\u0009\u000a\u000d\u0020-\uD7FF\uE000-\uFFFD]");
        private static IList<string> systemFilesToExlude = new List<string>()
                                                            {
                                                                "desktop.ini",
                                                                "thumbs.db",
                                                                ".ds_store",
                                                                "icon\r",
                                                                ".dropbox",
                                                                ".dropbox.attr"
                                                            };

        /// <summary>
        /// Returns the application's installation directory.
        /// </summary>
        /// <returns>DirectoryInfo</returns>
        public static DirectoryInfo GetInstallDirectoryInfo()
        {
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            return new FileInfo(assemblyLocation).Directory;
        }

        public static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            // If the destination directory doesn't exist, create it. 
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location. 
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        /// <summary>
        /// Identify if path is a junction point.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>bool</returns>
        public static bool IsJunction(string path)
        {
            try
            {
                var attributes = File.GetAttributes(path);
                bool isJunction = (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
                return isJunction;
            }
            catch(Exception e)
            { throw e; }
        }

        /// <summary>
        /// Check if a file complies with the BigStash API restrictions for accepted file names.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>Enumerations.InvalidFileCategory</returns>
        public static Enumerations.FileCategory CheckFileApiRestrictions(string path)
        {
            try
            {
                // check the file name character count
                if (path.Count() > 260)
                {
                    return Enumerations.FileCategory.FileNameTooLong;
                }

                // check if the file does not exist.
                if (!File.Exists(path))
                {
                    throw new System.IO.FileNotFoundException();
                }

                // get the file name
                var fileName = Path.GetFileName(path).ToLower();

                // check if the file is in the ignored system files list.
                if (systemFilesToExlude.Contains(fileName))
                {
                    return Enumerations.FileCategory.IgnoredSystemFile;
                }

                // check if the filename ends with a period or a whitespace.
                if (fileName.EndsWith(" ") || fileName.EndsWith("."))
                {
                    return Enumerations.FileCategory.TrailingPeriodsOrWhiteSpaceInName;
                }

                // check if the filename starts with characters used in naming
                // temporary files. Such a file might not be an actual temporary file,
                // but it will be excluded nonetheless.
                if (fileName.StartsWith(@"~$") || fileName.StartsWith(@".~") ||
                    (fileName.StartsWith("~") && fileName.EndsWith(".tmp")))
                {
                    return Enumerations.FileCategory.TemporaryFile;
                }

                // check for the existence of invalid file name chars
                // OR if the name does not satisfy the pattern.
                if (fileName.IndexOfAny(invalidFileNameChars) > -1 ||
                    !xmlValidCharsRegEx.IsMatch(fileName))
                {
                    return Enumerations.FileCategory.InvalidCharacterInName;
                }

                // Get the file attributes.
                FileAttributes attributes = File.GetAttributes(path);

                // check if attributes point that the file is a temporary file.
                if ((attributes & FileAttributes.Temporary) == FileAttributes.Temporary)
                {
                    return Enumerations.FileCategory.TemporaryFile;
                }

                if ((attributes & FileAttributes.Offline) == FileAttributes.Offline)
                {
                    return Enumerations.FileCategory.UnsyncedOnlineFile;
                }

                // check if attributes point that the file is a junction point
                // of if it's a shortcut file.
                if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    return Enumerations.FileCategory.MetadataFile;
                }

                return Enumerations.FileCategory.Normal;
            }
            catch(Exception e)
            { throw e; }
        }

        /// <summary>
        /// Get the caller method's name.
        /// </summary>
        /// <param name="memberName"></param>
        /// <returns></returns>
        public static string GetCallerName([System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            return memberName;
        }

        /// <summary>
        /// Gets the MD5 hash of the given path and encodes it in Base64.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetMD5Hash(string path)
        {
            string hash = String.Empty;

            using (MD5 md5Hash = MD5.Create())
            {
                byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(path));

                // Create a new Stringbuilder to collect the bytes 
                // and create a string.
                StringBuilder sBuilder = new StringBuilder();

                // Loop through each byte of the hashed data  
                // and format each one as a hexadecimal string. 
                for (int i = 0; i < data.Length; i++)
                {
                    sBuilder.Append(data[i].ToString("x2"));
                }

                hash = sBuilder.ToString();
            }

            return hash;
        }

        /// <summary>
        /// Creates a zip file containing only the file from the path parameter and returns it's path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string CreateZipFile(string path)
        {
            try
            {
                // The path to save the zip file should point in the same directory as the original file to include in the zip.
                var zipPath = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".zip");

                // If a zip file with the same path exists, delete it.
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                using (ZipArchive newZipFile = ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
                {
                    newZipFile.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Optimal);
                }

                return zipPath;
            }
            catch(Exception)
            { throw; }
        }

        public static void CompressManifestToGZip(string path, ArchiveManifest manifest)
        {
            using (FileStream fs = File.Open(path, FileMode.Create))
            using (GZipStream gz = new GZipStream(fs, CompressionLevel.Optimal))
            using (StreamWriter sw = new StreamWriter(gz, Encoding.UTF8))
            using (JsonWriter jw = new JsonTextWriter(sw))
            
            {
                jw.Formatting = Formatting.Indented;

                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(jw, manifest);
            }
        }

        /// <summary>
        /// Calculate a capped exponential backoff.
        /// </summary>
        /// <param name="attempts"></param>
        /// <param name="minDelay"></param>
        /// <param name="maxDelay"></param>
        /// <returns></returns>
        public static int CalculateExponentialBackOff(int attempts, int minInterval, int maxInterval)
        {
            var exponential = minInterval * ((int)Math.Pow(2, attempts) - 1);

            if (exponential > maxInterval)
            {
                exponential = maxInterval;
            }

            return exponential;
        }

        #region shell32_code
        ///// <summary>
        ///// Check if path is a shortcut, either a .lnk file or a .appref-ms.
        ///// Other application specific shortcut files can also be identified.
        ///// </summary>
        ///// <param name="path"></param>
        ///// <returns>bool</returns>
        //private static bool IsShortcut(string path)
        //{
        //    try
        //    {
        //        if (!File.Exists(path))
        //        {
        //            throw new System.IO.FileNotFoundException();
        //        }

        //        string directory = Path.GetDirectoryName(path);
        //        string file = Path.GetFileName(path);

        //        // Shell32.Shell shell = new Shell32.Shell();
        //        // Shell32.Folder folder = shell.NameSpace(directory);

        //        //This call is needed to get the folder object from the Shell32 application under Windows 8.
        //        Shell32.Folder folder = GetShell32NameSpaceFolder(directory); 
                
        //        if (folder == null)
        //        {
        //            return false;
        //        }

        //        Shell32.FolderItem folderItem = folder.ParseName(file);

        //        if (folderItem != null)
        //        {
        //            return folderItem.IsLink;
        //        }

        //        return false;
        //    }
        //    catch(Exception e)
        //    { 
        //        // log
        //        throw e; 
        //    }
        //}

        ///// <summary>
        ///// This method is needed to get the folder object from the Shell32 application
        ///// under Windows 8.
        ///// </summary>
        ///// <param name="folder"></param>
        ///// <returns></returns>
        //private static Shell32.Folder GetShell32NameSpaceFolder(Object folder)
        //{
        //    Type shellAppType = Type.GetTypeFromProgID("Shell.Application");

        //    Object shell = Activator.CreateInstance(shellAppType);
        //    return (Shell32.Folder)shellAppType.InvokeMember("NameSpace",
        //    System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { folder });
        //}
        #endregion
    }
}
