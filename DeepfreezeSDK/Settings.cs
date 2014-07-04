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
        /// Check if a used is logged in the Deepfreeze client.
        /// ActiveUser and ActiveToken must be set for a positive reply.
        /// </summary>
        /// <returns>bool</returns>
        public bool IsLogged()
        {
            return this.ActiveUser != null && this.ActiveToken != null;
        }

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
