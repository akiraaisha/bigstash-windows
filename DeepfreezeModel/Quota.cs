using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DeepfreezeModel
{
    public class Quota
    {
        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("used")]
        public long Used { get; set; }

        public Quota() { }

        public Quota(JObject json)
        {
            this.Size = (long)json["size"];
            this.Used = (long)json["used"];
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
