using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DeepfreezeModel
{
    public class Archive
    {
        [JsonIgnore]
        public Enumerations.Status Status { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("checksum")]
        public string Checksum { get; set; }

        [JsonProperty("created")]
        public DateTime Created { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("upload")]
        public string UploadUrl { get; set; }

        public Archive() { }
        public Archive(JObject json)
        {
            this.Status = Enumerations.GetStatusFromString((string)json["status"]);
            this.Size = (long)json["size"];
            this.Key = (string)json["key"];
            this.Checksum = (string)json["checksum"];
            this.Created = (DateTime)json["created"];
            this.Title = (string)json["title"];
            this.Url = (string)json["url"];
            this.UploadUrl = (string)json["upload"];
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
