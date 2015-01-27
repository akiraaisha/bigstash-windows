using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

namespace DeepfreezeApp
{
    [Export(typeof(IRestartAppMessage))]
    public class RestartAppMessage : IRestartAppMessage
    {
        public bool DoGracefulRestart { get; set; }
        public bool ConfigureSettingsMigration { get; set; }
    }
}
