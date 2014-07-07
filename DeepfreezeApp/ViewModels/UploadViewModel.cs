using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using Caliburn.Micro;
using DeepfreezeSDK;
using DeepfreezeModel;

namespace DeepfreezeApp
{
    [Export(typeof(IUploadViewModel))]
    public class UploadViewModel : PropertyChangedBase, IUploadViewModel
    {
        #region fields
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private Archive _archive;
        private Upload _upload;
        private IList<string> _paths;
        #endregion

        #region constructor
        [ImportingConstructor]
        public UploadViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
        }

        #endregion

        #region properties

        public Archive Archive
        {
            get { return this._archive; }
            set
            {
                this._archive = value;
                NotifyOfPropertyChange(() => Archive);
            }
        }

        public Upload Upload
        {
            get { return this._upload; }
            set
            { 
                this._upload = value;
                NotifyOfPropertyChange(() => Upload);
            }
        }

        public IList<string> Paths
        {
            get { return this._paths; }
            set { this._paths = value; }
        }

        #endregion

        #region action methods

        public async Task GetArchive()
        {
            try
            {
                this.Archive = await this._deepfreezeClient.GetArchiveAsync(this.Upload.ArchiveUrl);
            }
            catch(Exception e)
            {

            }
        }

        public async Task Delete()
        {
            try
            {
                var deleteSuccess = await this._deepfreezeClient.DeleteUploadAsync(this.Upload);
            }
            catch(Exception e)
            {

            }
        }

        #endregion

        #region message handlers
        #endregion

        #region private methods
        #endregion
    }
}
