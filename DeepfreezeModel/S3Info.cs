using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeModel
{
    public class S3Info
    {
        [JsonProperty("bucket")]
        public string Bucket { get; set; }

        [JsonProperty("prefix")]
        public string Prefix { get; set; }

        [JsonProperty("token_expiration")]
        public DateTime TokenExpiration { get; set; }

        [JsonProperty("token_session")]
        public string TokenSession { get; set; }

        [JsonProperty("token_uid")]
        public string TokenUID { get; set; }

        [JsonProperty("token_secret_key")]
        public string TokenSecretKey { get; set; }

        [JsonProperty("token_access_key")]
        public string TokenAccessKey { get; set; }

        public S3Info() { }

        public S3Info(JObject json)
        {
            this.Bucket = (string)json["bucket"];
            this.Prefix = (string)json["bucket"];
            this.TokenExpiration = (DateTime)json["token_expiration"];
            this.TokenSession = (string)json["token_session"];
            this.TokenUID = (string)json["token_uid"];
            this.TokenSecretKey = (string)json["token_secret_key"];
            this.TokenAccessKey = (string)json["token_access_key"];
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
