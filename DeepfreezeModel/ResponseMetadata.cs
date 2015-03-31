using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BigStash.Model
{
    public class ResponseMetadata
    {
        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("next")]
        public string NextPageUri { get; set; }

        [JsonProperty("previous")]
        public string PreviousPageUri { get; set; }

        // use this to hold the etag header value.
        [JsonIgnore]
        public string Etag { get; set; }
    }
}
