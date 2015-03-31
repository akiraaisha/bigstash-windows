using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace BigStash.Model
{
    public static class LocalStorage
    {
        private static object _syncLock = new object();
        /// <summary>
        /// Write json to file, given a file path and a json string.
        /// If the file does not exist, a new file will be created.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="json"></param>
        /// <returns>bool</returns>
        public static bool WriteJson(string path, object json, Encoding encoding, bool handleExceptions = false)
        {
            lock (_syncLock)
            {

                // save the file to a temp file first before moving it to the path given as an argument.
                string temp = path + ".tmp";
                string backup = path + ".bak";
                try
                {
                    using (FileStream fs = File.Open(temp, FileMode.Create))
                    using (StreamWriter sw = new StreamWriter(fs, encoding))
                    using (JsonWriter jw = new JsonTextWriter(sw))
                    {
                        jw.Formatting = Formatting.Indented;

                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Serialize(jw, json);
                    }

                    // Create the desired file in path if it doesn't exist, because Replace will error if it doesn't exist.
                    if (!File.Exists(path))
                    {
                        File.WriteAllText(path, null);
                    }

                    // The temp file is created and saved. Use Replace to move its contents
                    // in the file saved at the path argument. If the file does not exist
                    // then it is created.
                    File.Replace(temp, path, backup, false);

                    // Delete the backup file.
                    if (File.Exists(backup))
                    {
                        File.Delete(backup);
                    }

                    // If the temp file didn't get deleted, just delete it.
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }

                    return true;

                }
                catch (Exception)
                {
                    if (handleExceptions)
                    {
                        return false;
                    }
                    else
                    { 
                        throw; 
                    }
                }
            }
        }
    }
}
