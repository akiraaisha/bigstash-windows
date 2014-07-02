using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeModel
{
    public class Upload
    {
        public string Url { get; set; }
        public string ArchiveUrl { get; set; }
        public DateTime Created { get; set; }
        public Enumerations.Status Status { get; set; }
        public string Comment { get; set; }
        public string S3 { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
