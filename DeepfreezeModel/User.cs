using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeModel
{
    public class User
    {
        /// <summary>
        /// User ID.
        /// </summary>
        [JsonProperty("id")]
        public int ID { get; set; }

        /// <summary>
        /// User's email.
        /// </summary>
        [JsonProperty("email")]
        public string Email { get; set; }

        /// <summary>
        /// Date when user joined the Deepfreeze service.
        /// </summary>
        [JsonProperty("date_joined")]
        public DateTime DateJoined { get; set; }

        /// <summary>
        /// User's Display Name.
        /// </summary>
        [JsonProperty("displayname")]
        public string DisplayName { get; set; }

        /// <summary>
        /// User's Archives.
        /// </summary>
        [JsonProperty("archives")]
        public List<Archive> Archives { get; set; }

        /// <summary>
        /// User's Quota.
        /// </summary>
        [JsonProperty("quota")]
        public Quota Quota { get; set; }

        /// <summary>
        /// Serialize User to JSON string
        /// </summary>
        /// <returns></returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
