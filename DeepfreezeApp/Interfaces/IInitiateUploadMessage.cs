using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DeepfreezeModel;

namespace DeepfreezeApp
{
    public interface IInitiateUploadMessage
    {
        Archive Archive { get; set; }

        IList<string> Paths { get; set; }
    }
}
