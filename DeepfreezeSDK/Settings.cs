using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using DeepfreezeModel;

namespace DeepfreezeSDK
{
    public class Settings
    {
        /// <summary>
        /// Current active Deepfreeze user.
        /// </summary>
        [JsonProperty("user")]
        public User ActiveUser { get; set; }

        /// <summary>
        /// Current active Deepfreeze token used for authorizing api requests.
        /// </summary>
        [JsonProperty("token")]
        public Token ActiveToken { get; set; }

        /// <summary>
        /// Current api endpoint. Change this to connect to stage DF server or production DF server.
        /// URL should be like "https://www.deepfreeze.io/api/v1/". If the url misses the ending
        /// "/" character, the public setter takes care of it. :)
        /// </summary>
        [JsonProperty("api_endpoint")]
        public string ApiEndpoint { get; set; }

        /// <summary>
        /// Serialize Settings to json string.
        /// </summary>
        /// <returns>string</returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
