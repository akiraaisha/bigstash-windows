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
    [Export(typeof(IUploadManagerViewModel))]
    public class UploadManagerViewModel : IUploadManagerViewModel
    {
        #region fields
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;
        #endregion

        #region constructor
        [ImportingConstructor]
        public UploadManagerViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
        }

        #endregion

        #region properties
        #endregion

        #region action methods
        #endregion

        #region message handlers
        #endregion

        #region private methods
        #endregion
    }
}
