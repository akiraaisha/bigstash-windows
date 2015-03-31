using DeepfreezeModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigStash.WPF
{
    public interface IUploadActionMessage
    {
        Enumerations.UploadAction UploadAction { get; set; }

        UploadViewModel UploadVM { get; set; }
    }
}
