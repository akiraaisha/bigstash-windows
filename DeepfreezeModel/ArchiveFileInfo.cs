using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeModel
{
    public class ArchiveFileInfo
    {
        /// <summary>
        /// File name for the S3 Object.
        /// </summary>
        [JsonProperty("file_name")]
        public string FileName { get; set; }

        /// <summary>
        /// Key name for the S3 Object.
        /// </summary>
        [JsonProperty("key_name")]
        public string KeyName { get; set; }

        /// <summary>
        /// File path to upload.
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
        /// True if file has finished uploading after
        /// a successful Complete Upload Request
        /// </summary>
        [JsonProperty("uploaded")]
        public bool IsUploaded { get; set; }

        /// <summary>
        /// True if file has finished uploading after
        /// a successful Complete Upload Request
        /// </summary>
        [JsonProperty("progress")]
        public long Progress { get; set; }

        /// <summary>
        /// True if file has finished uploading after
        /// a successful Complete Upload Request
        /// </summary>
        [JsonProperty("uploadid")]
        public string UploadId { get; set; }

        /// <summary>
        /// Serialize ArchiveFileInfo to JSON string
        /// </summary>
        /// <returns></returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
