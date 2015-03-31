using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BigStash.Model
{
    public class Token
    {
        /// <summary>
        /// Authorization token for communicating with
        /// the Deepfreeze API and Amazon S3 API.
        /// </summary>
        [JsonProperty("key")]
        public string Key { get; set; }

        /// <summary>
        /// Unique token name.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Secret key.
        /// </summary>
        [JsonProperty("secret")]
        public string Secret { get; set; }

        /// <summary>
        /// Token creation date.
        /// </summary>
        [JsonProperty("created")]
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// Token url.
        /// </summary>
        [JsonProperty("url")]
        public string Url { get; set; }

        /// <summary>
        /// Serialize TokenPostResponse to JSON string
        /// </summary>
        /// <returns></returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
