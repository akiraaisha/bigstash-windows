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
using DeepfreezeSDK.Exceptions;
using DeepfreezeModel;
using System.Windows.Input;
using System.Windows;

namespace BigStash.WPF
{
    [Export(typeof(ILoginViewModel))]
    public class LoginViewModel : Screen, ILoginViewModel, IHandle<IInternetConnectivityMessage>
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

        public bool IsInternetConnected
        { get { return this._deepfreezeClient.IsInternetConnected; } }

        public string ConnectButtonTooltipText
        {
            get
            {
                if (this.IsInternetConnected)
                    return null;
                else
                    return Properties.Resources.ConnectButtonDisabledTooltipText;
            }
        }

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
                if (!this._deepfreezeClient.IsInternetConnected)
                    throw new Exception("Can't login without an active Internet connection.");

                IsBusy = true;

                _log.Info("Connecting user with email \"" + this.UsernameInput + "\".");

                var authorizationString = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format("{0}:{1}", UsernameInput, PasswordInput)));
                var token = await _deepfreezeClient.CreateTokenAsync(authorizationString);

                if (token == null)
                {
                    _log.Warn("CreateTokenAsync returned null.");
                    throw new Exception("CreateTokenAsync returned null.");
                }

                _log.Info("Created a new BigStash token.");

                // After creating a new token, set it to be used as default in DeepfreezeClient.
                this._deepfreezeClient.Settings.ActiveToken = token;

                // Make sure that the token satisfies the authorization level needed for GET /user/
                var user = await _deepfreezeClient.GetUserAsync();

                if (user == null)
                {
                    _log.Warn("GetUserAsync returned null.");
                    throw new Exception("GetUserAsync returned null.");
                }

                // Since the user response contains a valid User, 
                // then update the DeepfreezeClient settings.
                this._deepfreezeClient.Settings.ActiveUser = user;

                // Since ActiveToken and ActiveUser are not null, 
                // the user is considered as logged into the DeepfreezeClient.

                _log.Info("Fetched User is valid, saving to \"" + Properties.Settings.Default.SettingsFilePath + "\".");

                // Save preferences file.
                var writeSuccess = LocalStorage.WriteJson(Properties.Settings.Default.SettingsFilePath, this._deepfreezeClient.Settings, Encoding.UTF8, true);

                if (!writeSuccess)
                {
                    var windowManager = IoC.Get<IWindowManager>();
                    await windowManager.ShowMessageViewModelAsync("There was an error while trying to save your settings. " +
                        "Please make sure that you have enough free space on your hard drive.\n\n" +
                        "You may have to reconnect your BigStash account the next time you run the BigStash application.", "Error saving settings", 
                        MessageBoxButton.OK);
                }

                this.ClearErrors();

                // Publish LoginSuccess Message
                await this._eventAggregator.PublishOnUIThreadAsync(IoC.Get<ILoginSuccessMessage>());
            }
            catch (Exception e) 
            {
                this.HasLoginError = true;
                this.LoginError = Properties.Resources.ErrorConnectingGenericText;

                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");

                if (e is BigStashException)
                {
                    var bgex = e as BigStashException;

                    switch (bgex.StatusCode)
                    {
                        case System.Net.HttpStatusCode.Unauthorized:
                            this.LoginError = Properties.Resources.UnauthorizedExceptionMessage;
                            break;
                    }
                }
                else
                {
                    if (!this._deepfreezeClient.IsInternetConnected)
                        this.LoginError = Properties.Resources.ErrorConnectingWithoutInternetText;
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

        #region message_handlers

        public void Handle(IInternetConnectivityMessage message)
        {
            if (message != null)
            {
                NotifyOfPropertyChange(() => this.IsInternetConnected);
                NotifyOfPropertyChange(() => this.ConnectButtonTooltipText);
            }
        }

        #endregion

        #region events

        protected override void OnActivate()
        {
            this.Reset();
            this._eventAggregator.Subscribe(this);
            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            this.Reset();
            this._eventAggregator.Unsubscribe(this);
            base.OnDeactivate(close);
        }

        #endregion
    }
}
