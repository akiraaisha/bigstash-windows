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
        //  "s3" : {
        //"bucket": "dfstage",
        //"prefix": "/upload/2014-06-30-17-55-48/",
        //"token_expiration" : "2014-07-01T18:02:25Z",
        //"token_session" : "AQoDYXdzEPP//////////wEakANwN+SZP5IgC9vszUVqlu3hRCny7zqfctTnRcIr2iHmVONVSWjAUz+Wh/POni6xqbCKilKuwGioHmHFXOwMdEK8EsKW0pfbIf7K1qTZ7uzryFmAnXKuInhRvg0gzCeNn3HRyFHydmxGfw8cORZi3VO9CKfRikcdQMQrm3rf1TXlvUCxZwtdRbrui8k1y9Xl/GKUOWMemcvUkeahKUdJDNGIoIaIfC3oUh+FXP4ynVHjfKyCpniiK4yvnqisDPipCRj0qn1l6gcycRqBxjdlPaVmqqtdutC+tWrC/65YSqWzlGBlSBZhUoIUurGbrUijurJCM+C81YArixDKFs/s20cqAg/BnpR/b4ivX9yAgt34fmlc87SPSty2/JTnYdQiELjkOphyW17C2SDA0yYMwJCUTbtYE/igHYK8Ev0Om/OuIFG0BtHudWRsCCgyqIFeaPxzoQEoOfH1X4cJ1jxaPpiVSzDURhSVpjMx0fwkb64E999sD3e4iUV+weW9sO/bA+Jm3k7xxZX/1SA925K4bs6EILHMxp0F",
        //"token_uid" : "lacli",
        //"token_secret_key" : "N8yLBiygVKaF7G7BalqbkpuvA4PUEpO8k127mHrL",
        //"token_access_key" : "ASIAJCM4TZOZJQDDTMFQ"
        //}
    }
}
