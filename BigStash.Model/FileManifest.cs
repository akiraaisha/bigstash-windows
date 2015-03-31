using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BigStash.Model
{
    public class FileManifest
    {
        /// <summary>
        /// Key name for the S3 Object.
        /// </summary>
        [JsonProperty("key_name")]
        public string KeyName { get; set; }

        /// <summary>
        /// Original file path.
        /// </summary>
        [JsonProperty("file_path")]
        public string FilePath { get; set; }

        /// <summary>
        /// File size on disk.
        /// </summary>
        [JsonProperty("size")]
        public long Size { get; set; }

        /// <summary>
        /// Last modified date.
        /// </summary>
        [JsonProperty("last_modified")]
        public DateTime LastModified { get; set; }

        /// <summary>
        /// File Hash.
        /// </summary>
        [JsonProperty("md5")]
        public string MD5 { get; set; }

        /// <summary>
        /// Serialize FileManifest to JSON string
        /// </summary>
        /// <returns></returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
