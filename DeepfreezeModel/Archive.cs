using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DeepfreezeModel
{
    public class Archive
    {
        public Enumerations.Status Status { get; set; }
        public long Size { get; set; }
        public string Key { get; set; }
        public string Checksum { get; set; }
        public DateTime Created { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string Upload { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
