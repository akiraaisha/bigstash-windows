using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

namespace BigStash.WPF
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(IRemoveUploadViewModelMessage))]
    public class RemoveUploadViewModelMessage : IRemoveUploadViewModelMessage
    {
        public UploadViewModel UploadVMToRemove { get; set; }
    }
}
