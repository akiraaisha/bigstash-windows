using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeModel
{
    public class Token
    {
        /// <summary>
        /// Authorization token for communicating with
        /// the Deepfreeze API and Amazon S3 API.
        /// </summary>
        public string Key { get; set; }
        /// <summary>
        /// Unique token ID.
        /// </summary>
        public int ID { get; set; }
        /// <summary>
        /// Token creation date.
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Serialize Token to JSON string
        /// </summary>
        /// <returns></returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
