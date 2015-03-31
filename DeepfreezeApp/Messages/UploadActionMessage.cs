using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

namespace BigStash.WPF
{
    [Export(typeof(IUploadActionMessage))]
    public class UploadActionMessage : IUploadActionMessage
    {
        public DeepfreezeModel.Enumerations.UploadAction UploadAction { get; set; }

        public UploadViewModel UploadVM { get; set; }
    }
}
