using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

namespace BigStash.SDK.Retry
{
    public class RetryDelegatingHanlder : DelegatingHandler
    {
        public RetryPolicy RetryPolicy { get; set; }
        public int RetryCount { get; set; }

        public RetryDelegatingHanlder(HttpMessageHandler innerHandler, int retryCount)
            : base(innerHandler)
        {
            this.RetryPolicy = CustomRetryPolicyFactory.MakeHttpRetryPolicy(retryCount);
            this.RetryCount = retryCount;
        }

        /// <summary>
        /// HttpClient.SendAsync() override implementing retry logic.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            HttpResponseMessage responseMessage = null;
            var currentRetryCount = 0;

            RetryPolicy.Retrying += (sender, args) =>
            {
                currentRetryCount = args.CurrentRetryCount;
            };

            try
            {
                await RetryPolicy.ExecuteAsync(async () =>
                {
                    responseMessage = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                    var errorType = HelperMethods.IsFailedStatusCodeServiceFault(responseMessage.StatusCode);
                    if (errorType != Exceptions.ErrorType.NotSet)
                    {
                        string errorTypeSubString = String.Empty;

                        if (errorType == Exceptions.ErrorType.Service)
                            errorTypeSubString = "server";
                        else if (errorType == Exceptions.ErrorType.Client)
                            errorTypeSubString = "client";

                        throw new Exceptions.BigStashException(string.Format("Response status code {0} - {1} indicates " + errorTypeSubString + " error.", (int)responseMessage.StatusCode, responseMessage.StatusCode.ToString()))
                        {
                            Request = request,
                            StatusCode = responseMessage.StatusCode,
                            CurrentRetryCount = currentRetryCount,
                            ErrorType = errorType
                        };
                    }

                    return responseMessage;
                }, cancellationToken).ConfigureAwait(false);

                return responseMessage;
            }
            catch(Exceptions.BigStashException bgex)
            {
                if (bgex.CurrentRetryCount >= this.RetryCount)
                {
                    // write to log???
                }

                if (responseMessage != null && bgex.ErrorType == Exceptions.ErrorType.NotSet)
                {
                    return responseMessage;
                }

                throw;
            }
            catch(Exception)
            {
                if (responseMessage != null)
                {
                    return responseMessage;
                }

                throw;
            }
        }
    }
}
