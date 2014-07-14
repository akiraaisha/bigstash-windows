using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using Caliburn.Micro;
using DeepfreezeSDK;

namespace DeepfreezeApp
{
    [Export(typeof(IPreferencesViewModel))]
    public class PreferencesViewModel : Screen, IPreferencesViewModel
    {
        #region members
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private IUserViewModel _userVM = IoC.Get<IUserViewModel>();

        private bool _isOpen;
        #endregion

        #region constructors
        public PreferencesViewModel() { }

        public PreferencesViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
        }
        #endregion

        #region properties
        public UserViewModel UserVM
        {
            get { return _userVM as UserViewModel; }
        }

        public bool IsOpen
        {
            get { return this._isOpen; }
            set { this._isOpen = value; NotifyOfPropertyChange(() => IsOpen); }
        }
        #endregion

        #region methods

        #endregion

        protected override void OnActivate()
        {
            base.OnActivate();
        }
    }
}
