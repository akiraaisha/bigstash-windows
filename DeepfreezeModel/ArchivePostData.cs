using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeModel
{
    public class ArchivePostData
    {
        [JsonProperty("size")]
        public long Size { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
