using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

namespace DeepfreezeApp
{
    [Export(typeof(IInternetConnectivityMessage))]
    public class InternetConnectivityMessage : IInternetConnectivityMessage
    {
        public bool IsConnected { get; set; }
    }
}
