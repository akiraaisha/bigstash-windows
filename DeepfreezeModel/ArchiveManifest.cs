using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DeepfreezeModel
{
    public class ArchiveManifest
    {
        private IList<FileManifest> _files = new List<FileManifest>();

        /// <summary>
        /// Archive ID.
        /// </summary>
        [JsonProperty("archiveid")]
        public string ArchiveID { get; set; }

        /// <summary>
        /// User ID.
        /// </summary>
        [JsonProperty("userid")]
        public int UserID { get; set; }

        /// <summary>
        /// Files array.
        /// </summary>
        [JsonProperty("files")]
        public IList<FileManifest> Files
        {
            get { return this._files; }
            set { this._files = value; }
        }

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
