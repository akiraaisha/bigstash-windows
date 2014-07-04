using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DeepfreezeModel
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
        /// Unique token ID.
        /// </summary>
        [JsonProperty("id")]
        public int ID { get; set; }
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
        /// Serialize TokenPostResponse to JSON string
        /// </summary>
        /// <returns></returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
