using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace DeepfreezeModel
{
    public class LocalUpload
    {
        private long _progress = 0;

        [JsonIgnore]
        public string SavePath { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("progress")]
        public long Progress
        {
            get { return this._progress; }
            set { this._progress = (long)value; }
        }

        [JsonProperty("user_paused")]
        public bool UserPaused { get; set; }

        [JsonProperty("archive_files_info")]
        public IList<ArchiveFileInfo> ArchiveFilesInfo { get; set; }
    }
}
