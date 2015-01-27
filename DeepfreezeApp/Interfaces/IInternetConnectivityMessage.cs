using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeApp
{
    public interface IInternetConnectivityMessage
    {
        bool IsConnected { get; set; }
    }
}
