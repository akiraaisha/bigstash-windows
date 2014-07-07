using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace DeepfreezeModel
{
    [JsonObject("local_uploads")]
    public class LocalUploads
    {
        private IList<string> _urls;
        [JsonProperty("urls")]
        public IList<string> Urls
        {
            get { return this._urls; }
            set { this._urls = value; }
        }
    }
}
