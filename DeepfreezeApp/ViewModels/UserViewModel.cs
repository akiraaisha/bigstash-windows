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

        public double UsedPercentage
        {
            get 
            {
                var percentage = (double)(this._deepfreezeClient.Settings.ActiveUser.Quota.Used /
                     this._deepfreezeClient.Settings.ActiveUser.Quota.Size) * 100;
                return percentage;    
            }
        }

        public string SizeInformation
        { 
            get 
            {
                double used = (double)this._deepfreezeClient.Settings.ActiveUser.Quota.Used;
                double total = (double)this._deepfreezeClient.Settings.ActiveUser.Quota.Size;

                var sb = new StringBuilder();
                sb.Append(LongToSizeString.ConvertToString(total - used));
                sb.Append(@" Free / ");
                sb.Append(LongToSizeString.ConvertToString(used));
                sb.Append(@" Used / ");
                sb.Append(LongToSizeString.ConvertToString(total));
                sb.Append(@" Total");

                return sb.ToString();
            } 
        }

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
