﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using Caliburn.Micro;
using DeepfreezeSDK;
using DeepfreezeModel;
using System.IO;

namespace DeepfreezeApp
{
    [Export(typeof(IUserViewModel))]
    public class UserViewModel : Screen, IUserViewModel, IHandleWithTask<IRefreshUserMessage>
    {
        #region members

        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private bool _isBusy = false;
        private string _errorMessage;

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

        public bool IsBusy
        {
            get { return this._isBusy; }
            set { this._isBusy = value; }
        }

        public string ErrorMessage
        {
            get { return this._errorMessage; }
            set { this._errorMessage = value; NotifyOfPropertyChange(() => this.ErrorMessage); }
        }

        public User ActiveUser
        {
            get { return this._deepfreezeClient.Settings.ActiveUser; }
        }

        public string ActiveUserHeader
        { get { return Properties.Resources.ActiveUserHeader; } }

        public string QuotaHeader
        { get { return Properties.Resources.QuotaHeader; } }

        public string LogoutString
        { get { return Properties.Resources.DisconnectButtonContent; } }

        public double UsedPercentage
        {
            get 
            {
                var percentage = ((double)this.ActiveUser.Quota.Used / this.ActiveUser.Quota.Size) * 100;
                return percentage;    
            }
        }

        public string SizeInformation
        { 
            get 
            {
                double used = (double)this.ActiveUser.Quota.Used;
                double total = (double)this.ActiveUser.Quota.Size;

                var sb = new StringBuilder();
                sb.Append(LongToSizeString.ConvertToString(total - used));
                sb.Append(Properties.Resources.FreeText);
                sb.Append(LongToSizeString.ConvertToString(used));
                sb.Append(Properties.Resources.UsedText);
                sb.Append(LongToSizeString.ConvertToString(total));
                sb.Append(Properties.Resources.TotalText);

                return sb.ToString();
            } 
        }

        #endregion

        #region methods

        public async Task RefreshUser()
        {
            this.IsBusy = true;
            try
            {
                var user = await this._deepfreezeClient.GetUserAsync();

                if (user != null)
                {
                    this._deepfreezeClient.Settings.ActiveUser = user;
                }
            }
            catch (Exception e) 
            {
                this.ErrorMessage = e.Message;
            }
            finally 
            { 
                this.IsBusy = false;
                this.Refresh();
            }
        }

        public void Logout()
        {
            try
            {
                // Finally publish a message to notify for the login change.
                // ShellViewModel is responsible for handling the message and activating the LoginViewModel.
                this._eventAggregator.PublishOnCurrentThread(IoC.Get<ILogoutMessage>());
            }
            catch (Exception e) { }
        }

        #endregion

        #region message_handlers

        public async Task Handle(IRefreshUserMessage message)
        {
            await this.RefreshUser();
        }

        #endregion

        #region private_methods
        #endregion

        #region events

        protected override void OnActivate()
        {
            this._eventAggregator.Subscribe(this);

            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            this._eventAggregator.Unsubscribe(this);

            base.OnDeactivate(close);
        }

        #endregion
    }
}
