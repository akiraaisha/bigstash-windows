using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BigStash.Model
{
    public class PartInfo
    {
        /// <summary>
        /// ID is the part number, used to identify each part
        /// and keep track of the parts' order.
        /// </summary>
        public int ID { get; set; }
        /// <summary>
        /// True if part has finished uploading
        /// and received an etag value from the upload response.
        /// </summary>
        public bool IsUploaded { get; set; }
        /// <summary>
        /// MD5 hash included in the UploadPartResponse.
        /// </summary>
        public string Etag { get; set; }

        /// <summary>
        /// Serialize UploadPartInfo to JSON string
        /// </summary>
        /// <returns></returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
