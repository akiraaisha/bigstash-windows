using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DeepfreezeModel;

namespace BigStash.WPF
{
    public interface IInitiateUploadMessage
    {
        Archive Archive { get; set; }

        IList<ArchiveFileInfo> ArchiveFilesInfo { get; set; }
    }
}
