using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

namespace DeepfreezeApp
{
    [Export(typeof(IRemoveUploadViewModel))]
    public class RemoveUploadViewModelMessage : IRemoveUploadViewModel
    {
        public UploadViewModel UploadVMToRemove { get; set; }
    }
}
