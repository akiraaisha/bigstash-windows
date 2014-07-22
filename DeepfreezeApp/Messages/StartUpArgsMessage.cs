using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

namespace DeepfreezeApp.Messages
{
    [Export(typeof(IStartUpArgsMessage))]
    public class StartUpArgsMessage : IStartUpArgsMessage
    {
        public string StartUpArgument { get; set; }
    }
}
