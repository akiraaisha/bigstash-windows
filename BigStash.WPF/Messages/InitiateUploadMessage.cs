using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using BigStash.Model;

namespace BigStash.WPF
{
    [Export(typeof(IInitiateUploadMessage))]
    public class InitiateUploadMessage : IInitiateUploadMessage
    {
        public Archive Archive { get; set; }

        public IList<ArchiveFileInfo> ArchiveFilesInfo { get; set; }
    }
}
