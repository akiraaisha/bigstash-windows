using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DeepfreezeModel
{
    public class Notification
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("verb")]
        public string Verb { get; set; }

        [JsonProperty("created")]
        public DateTime CreationDate { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }
}
