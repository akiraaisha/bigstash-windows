using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace DeepfreezeModel
{
    public static class LocalStorage
    {
        /// <summary>
        /// Write json to file, given a file path and a json string.
        /// If the file does not exist, a new file will be created.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="json"></param>
        /// <returns>bool</returns>
        public static bool WriteJson(string path, object json, Encoding encoding)
        {
            try
            {
                using (FileStream fs = File.Open(path, FileMode.Create))
                using (StreamWriter sw = new StreamWriter(fs, encoding))
                using (JsonWriter jw = new JsonTextWriter(sw))
                {
                    jw.Formatting = Formatting.Indented;

                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(jw, json);

                    return true;
                }
            }
            catch (Exception e)
            { throw e; }
        }
    }
}
