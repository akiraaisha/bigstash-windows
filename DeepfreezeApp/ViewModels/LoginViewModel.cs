using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Diagnostics;

using Caliburn.Micro;

using DeepfreezeSDK;
using DeepfreezeModel;

namespace DeepfreezeApp
{
    [Export(typeof(ILoginViewModel))]
    public class LoginViewModel : PropertyChangedBase, ILoginViewModel
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private bool _isBusy;
        private string _usernameInput;
        private string _passwordInput;

        public LoginViewModel() { }

        [ImportingConstructor]
        public LoginViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set { _isBusy = value; NotifyOfPropertyChange(() => IsBusy); }
        }

        public string UsernameInput
        {
            get
            {
                return _usernameInput;
            }
            set
            {
                _usernameInput = value;
                NotifyOfPropertyChange(() => UsernameInput);
            }
        }

        public string PasswordInput
        {
            get
            {
                return _passwordInput;
            }
            set
            {
                _passwordInput = value;
                NotifyOfPropertyChange(() => PasswordInput);
            }
        }

        public string LoginString
        {
            get { return Properties.Resources.LoginButtonContent; }
        }

        public async Task Login()
        {
            try
            {
                var authorizationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", UsernameInput, PasswordInput)));
                var token = await _deepfreezeClient.CreateTokenAsync(authorizationString);

                if (token == null) throw new Exception();

                // After creating a new token, set it to be used as default in DeepfreezeClient.

                // First instatiate a new Settings object with the new token.
                var settings = new Settings() { ActiveToken = token };

                // DeepfreezeClient should now use the new settings.
                _deepfreezeClient.Settings = settings;

                // Make sure that the token satisfies the authorization level needed for GET /user/
                var user = await _deepfreezeClient.GetUserAsync();

                if (user == null) throw new Exception();

                // Since the user response contains a valid User, 
                // then update the DeepfreezeClient settings.
                _deepfreezeClient.Settings.ActiveUser = user;

                // Since ActiveToken and ActiveUser are not null, 
                // the user is considered as logged into the DeepfreezeClient.
                
                // Save preferences file.
                LocalStorage.WriteJson(Properties.Settings.Default.SettingsFilePath, settings);
            }
            catch (Exception e) { }
        }

        public void RetrievePassword(PasswordBox pwdBox)
        {
            if (pwdBox != null)
                this.PasswordInput = pwdBox.Password;
        }

        public void Remember()
        {
            Process.Start(Properties.Resources.RememberPasswordURL);
        }
    }
}
