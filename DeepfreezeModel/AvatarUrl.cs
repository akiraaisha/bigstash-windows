using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DeepfreezeModel
{
    [JsonObject("avatar")]
    public class AvatarUrl
    {
        [JsonProperty("avatar80")]
        public string Large { get; set; }

        [JsonProperty("avatar48")]
        public string Medium { get; set; }

        [JsonProperty("avatar22")]
        public string Small { get; set; }
    }
}
