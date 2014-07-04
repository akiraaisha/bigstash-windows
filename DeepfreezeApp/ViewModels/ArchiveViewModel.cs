using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using Caliburn.Micro;
using DeepfreezeSDK;
using System.IO;

namespace DeepfreezeApp
{
    [Export(typeof(IArchiveViewModel))]
    public class ArchiveViewModel : IArchiveViewModel
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        public void ChooseFolder()
        {
            // Show the FolderBrowserDialog.
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    var dir = dialog.SelectedPath;
                    //var dirInfo = new DirectoryInfo(dir);
                }
                catch (Exception e)
                {
                    _eventAggregator.Publish(e, null);
                }
            }
        }

        [ImportingConstructor]
        public ArchiveViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
        }
    }
}
