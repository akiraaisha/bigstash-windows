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
    [Export(typeof(IUserViewModel))]
    public class UserViewModel : PropertyChangedBase, IUserViewModel
    {
        #region members
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private string _logoutString;
        #endregion

        #region constructors
        public UserViewModel() { }

        [ImportingConstructor]
        public UserViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
        }
        #endregion

        #region properties

        public User ActiveUser
        {
            get { return this._deepfreezeClient.Settings.ActiveUser; }
        }

        public string ActiveUserHeader
        { get { return Properties.Resources.ActiveUserHeader; } }

        public string QuotaHeader
        { get { return Properties.Resources.QuotaHeader; } }

        public string LogoutString
        { get { return Properties.Resources.LogoutButtonContent; } }
        #endregion

        #region methods
        public async Task Logout()
        {
            try
            {

            }
            catch (Exception e) { }
        }
        #endregion
    }
}
