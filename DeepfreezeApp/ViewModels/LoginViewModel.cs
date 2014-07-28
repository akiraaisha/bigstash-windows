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
using System.Windows.Input;

namespace DeepfreezeApp
{
    [Export(typeof(ILoginViewModel))]
    public class LoginViewModel : Screen, ILoginViewModel
    {
        #region members

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(LoginViewModel));
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private bool _isBusy = false;
        private bool _hasUsernameError = false;
        private bool _hasPasswordError = false;
        private bool _hasLoginError = false;

        private string _usernameInput;
        private string _passwordInput;

        private string _usernameError;
        private string _passwordError;
        private string _loginError;

        #endregion

        #region constructors

        public LoginViewModel() { }

        [ImportingConstructor]
        public LoginViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
        }

        #endregion

        #region properties

        public bool IsBusy
        {
            get { return _isBusy; }
            set { _isBusy = value; NotifyOfPropertyChange(() => IsBusy); }
        }

        public string ConnectHeader
        {
            get { return Properties.Resources.ConnectHeaderText; }
        }

        public bool HasUsernameError
        {
            get { return _hasUsernameError; }
            set { _hasUsernameError = value; NotifyOfPropertyChange(() => HasUsernameError); }
        }

        public bool HasPasswordError
        {
            get { return _hasPasswordError; }
            set { _hasPasswordError = value; NotifyOfPropertyChange(() => HasPasswordError); }
        }

        public bool HasLoginError
        {
            get { return _hasLoginError; }
            set { _hasLoginError = value; NotifyOfPropertyChange(() => HasLoginError); }
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

        public string UsernameHelper
        { get { return Properties.Resources.UsernameTextboxHelper; } }

        public string PasswordHelper
        { get { return Properties.Resources.PasswordTextboxHelper; } }

        public string LoginString
        {
            get { return Properties.Resources.ConnectButtonContent; }
        }

        public string UsernameError
        {
            get
            {
                return _usernameError;
            }
            set
            {
                _usernameError = value;
                NotifyOfPropertyChange(() => UsernameError);
            }
        }

        public string PasswordError
        {
            get
            {
                return _passwordError;
            }
            set
            {
                _passwordError = value;
                NotifyOfPropertyChange(() => PasswordError);
            }
        }

        public string LoginError
        {
            get
            {
                return _loginError;
            }
            set
            {
                _loginError = value;
                NotifyOfPropertyChange(() => LoginError);
            }
        }

        public string SetPasswordText
        { get { return Properties.Resources.SetPasswordText; } }

        #endregion

        #region action methods

        /// <summary>
        /// Validate the input boxes and proceed with creating a new Deepfreeze Token to use with
        /// DeepfreezeClient. Validate the new token by fetching the User resource and providing the 
        /// response as the Deepfreeze's ActiveUser property. Finally, save the local preferences file
        /// and publish a LoginSuccessMessage for the ShellViewModel to handle.
        /// </summary>
        /// <returns></returns>
        public async Task Connect()
        {
            if (!Validate())
                return;

            try
            {
                IsBusy = true;

                _log.Info("Connecting user with email \"" + this.UsernameInput + "\".");

                var authorizationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", UsernameInput, PasswordInput)));
                var token = await _deepfreezeClient.CreateTokenAsync(authorizationString);

                if (token == null)
                {
                    _log.Warn("CreateTokenAsync returned null.");
                    throw new Exception();
                }

                _log.Info("Created a new Deepfreeze token.");

                // After creating a new token, set it to be used as default in DeepfreezeClient.
                this._deepfreezeClient.Settings.ActiveToken = token;

                // Make sure that the token satisfies the authorization level needed for GET /user/
                var user = await _deepfreezeClient.GetUserAsync();

                if (user == null)
                {
                    _log.Warn("GetUserAsync returned null.");
                    throw new Exception();
                }

                // Since the user response contains a valid User, 
                // then update the DeepfreezeClient settings.
                this._deepfreezeClient.Settings.ActiveUser = user;

                // Since ActiveToken and ActiveUser are not null, 
                // the user is considered as logged into the DeepfreezeClient.

                _log.Info("Fetched User is valid, saving to \"" + Properties.Settings.Default.SettingsFilePath + "\".");

                // Save preferences file.
                await Task.Run(() => LocalStorage.WriteJson(Properties.Settings.Default.SettingsFilePath, this._deepfreezeClient.Settings, Encoding.ASCII))
                    .ConfigureAwait(false);

                this.ClearErrors();

                // Publish LoginSuccess Message
                this._eventAggregator.PublishOnCurrentThread(IoC.Get<ILoginSuccessMessage>());
            }
            catch (Exception e) 
            {
                HasLoginError = true;
                LoginError = e.Message;

                if (e is Exceptions.DfApiException)
                {
                    var response = ((Exceptions.DfApiException)e).HttpResponse;

                    switch (response.StatusCode)
                    {
                        case System.Net.HttpStatusCode.Unauthorized:
                            LoginError = Properties.Resources.UnauthorizedExceptionMessage;
                            break;
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Handle the PasswordBox TextChanged event and retrieve the typed password.
        /// </summary>
        /// <param name="pwdBox"></param>
        public void RetrievePassword(PasswordBox pwdBox)
        {
            if (pwdBox != null)
                this.PasswordInput = pwdBox.Password;
        }

        /// <summary>
        /// Open the Deepfreeze account page.
        /// </summary>
        public void OpenAccountPage()
        {
            Process.Start(Properties.Resources.SetPasswordURL);
        }

        /// <summary>
        /// Open the remember password Deepfreeze page.
        /// </summary>
        public void ForgotPassword()
        {
            Process.Start(Properties.Resources.ForgotPasswordURL);
        }

        public void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox)
            {
                var tb = sender as TextBox;

                if (tb == null)
                {
                    return;
                }

                if (!tb.IsKeyboardFocusWithin)
                {
                    tb.SelectAll();
                    e.Handled = true;
                    tb.Focus();
                }
            }
                
            else if (sender is PasswordBox)
            {
                var pb = sender as PasswordBox;

                if (pb == null)
                {
                    return;
                }

                if (!pb.IsKeyboardFocusWithin)
                {
                    pb.SelectAll();
                    e.Handled = true;
                    pb.Focus();
                }
            }
        }

        #endregion

        #region private methods

        /// <summary>
        /// Validate the username and password boxes.
        /// </summary>
        /// <returns></returns>
        private bool Validate()
        {
            ClearErrors();

            if (String.IsNullOrEmpty(UsernameInput))
            {
                HasUsernameError = true;
                UsernameError = Properties.Resources.UsernameError;
            }

            if (String.IsNullOrEmpty(PasswordInput))
            {
                HasPasswordError = true;
                PasswordError = Properties.Resources.PasswordError;
            }

            return !(HasUsernameError || HasPasswordError);
        }

        /// <summary>
        /// Clear all errors.
        /// </summary>
        private void ClearErrors()
        {
            HasUsernameError = false;
            HasPasswordError = false;
            HasLoginError = false;

            UsernameError = null;
            PasswordError = null;
            LoginError = null;
        }

        /// <summary>
        /// Clear this viewmodel's properties.
        /// </summary>
        private void Reset()
        {
            this.UsernameInput = null;
            this.PasswordInput = null;
            this.IsBusy = false;
        }

        #endregion

        #region events

        protected override void OnActivate()
        {
            this.Reset();
            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            this.Reset();
            base.OnDeactivate(close);
        }

        #endregion
    }
}
