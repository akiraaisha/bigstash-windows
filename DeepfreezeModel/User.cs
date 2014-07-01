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
        public int ID { get; set; }

        /// <summary>
        /// User's email.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Date when user joined the Deepfreeze service.
        /// </summary>
        public DateTime DateJoined { get; set; }

        /// <summary>
        /// User's Display Name.
        /// </summary>
        public string DisplayName { get; set; }

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
