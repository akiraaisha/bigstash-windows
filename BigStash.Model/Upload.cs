using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigStash.Model
{
    public class Upload
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("archive")]
        public string ArchiveUrl { get; set; }

        [JsonProperty("created")]
        public DateTime Created { get; set; }

        [JsonProperty("status")]
        public Enumerations.Status Status { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; }

        [JsonProperty("s3")]
        public S3Info S3 { get; set; }

        public Upload() { }

        public Upload(JObject json)
        {
            this.Url = (string)json["url"];
            this.ArchiveUrl = (string)json["archive"];
            this.Created = (DateTime)json["created"];
            this.Status = Enumerations.GetStatusFromString((string)json["status"]);
            this.Comment = (string)json["comment"];
            this.S3 = JsonConvert.DeserializeObject<S3Info>(json["s3"].ToString());
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
