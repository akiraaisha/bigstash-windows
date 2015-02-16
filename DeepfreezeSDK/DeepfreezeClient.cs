using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Runtime;
using System.Runtime.InteropServices;

using DeepfreezeModel;
using DeepfreezeSDK.Exceptions;
using DeepfreezeSDK.Retry;

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using log4net;

namespace DeepfreezeSDK
{
    [Export(typeof(IDeepfreezeClient))]
    public class DeepfreezeClient : IDeepfreezeClient
    {
        #region fields
        private static readonly ILog _log = LogManager.GetLogger(typeof(DeepfreezeClient));

        // strings representing http methods names in lower case;
        private readonly string GET = "GET";
        private readonly string POST = "POST";
        private readonly string PATCH = "PATCH";
        private readonly string DELETE = "DELETE";

        private readonly string ACCEPT = "application/vnd.deepfreeze+json";
        private readonly string AUTHORIZATION = @"keyId=""hmac-key-1"",algorithm=""hmac-sha256"",headers=""(request-line) host accept date""";

        private readonly string _tokenUri = "tokens/";
        private readonly string _userUri = "user/";
        private readonly string _uploadsUri = "uploads/";
        private readonly string _archivesUri = "archives/";
        private readonly string _notificationsUri = "notifications/";

        private readonly int FASTRETRY = 1;
        private readonly int LONGRETRY = 4;

        #endregion

        #region properties

        /// <summary>
        /// The Settings object holding the connected user, api token and endpoint.
        /// </summary>
        public Settings Settings { get; set; }

        /// <summary>
        /// Indicating whether an active internet connection is available or not.
        /// </summary>
        public bool IsInternetConnected
        {
            get
            {
                int desc;
                bool isConnected = false;
                isConnected = InternetGetConnectedState(out desc, 0);
                return isConnected;
            }
        }

        /// <summary>
        /// The version of the consuming application.
        /// </summary>
        public string ApplicationVersion { get; set; }

        #endregion

        #region constructors

        public DeepfreezeClient() { }

        #endregion

        #region methods_for_consuming_DF_API

        /// <summary>
        /// Check if DeepfreezeClient has a Settings property instatiated,
        /// and if the ActiveUser and ActiveToken are set. Return true if all stand.
        /// </summary>
        /// <returns>bool</returns>
        public bool IsLogged()
        {
            if (this.Settings == null)
                return false;
            else
            {
                return this.Settings.ActiveUser != null && this.Settings.ActiveToken != null;
            }
        }

        /// <summary>
        /// Return all active Deepfreeze authorization Tokens for the authorized user.
        /// </summary>
        /// <returns>Token</returns>
        public async Task<List<Token>> GetTokensAsync()
        {
            var request = CreateHttpRequestWithSignature(GET, _tokenUri);
            HttpResponseMessage response;

            try
            {
                using (var httpClient = this.CreateHttpClientWithRetryLogic(FASTRETRY))
                {
                    response = await httpClient.SendAsync(request).ConfigureAwait(false);
                }

                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                JObject json = JObject.Parse(content);

                if ((int)json["count"] > 0)
                {
                    var tokens = JsonConvert.DeserializeObject<List<Token>>(json["results"].ToString());
                    return tokens;
                }
                else
                {
                    throw new Exceptions.BigStashException("Server replied with success but response was empty.");
                }
            }
            catch (Exception e)
            {
                // If the caught exception is a BigStashException, then return it immediately
                // in order to be propagated to the higher caller as is, without wrapping it in
                // a new BigStashException instance.
                if (e is BigStashException)
                    throw;

                throw this.BigStashExceptionHandler(e);
            }
        }

        /// <summary>
        /// Create a new Deepfreeze API token using user credentials.
        /// </summary>
        /// <param name="authorizationString"></param>
        /// <returns>Token</returns>
        public async Task<Token> CreateTokenAsync(string authorizationString)
        {
            // Only for this request, the client uses basic auth with user credentials.
            // For every other authorized actions, a signed request should be sent.

            _log.Debug("Called CreateTokenAsync.");

            HttpResponseMessage response;

            var requestUri = new UriBuilder(this.Settings.ApiEndpoint + _tokenUri).Uri;
            var name = @"{""name"":""BigStash for Windows on " + Environment.MachineName + @"""}";
            var requestContent = new StringContent(name, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.deepfreeze+json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authorizationString);
            request.Content = requestContent;

            try
            {
                using (var httpClient = this.CreateHttpClientWithRetryLogic(FASTRETRY))
                {
                    response = await httpClient.SendAsync(request).ConfigureAwait(false);
                }

                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (content != null)
                {
                    var token = JsonConvert.DeserializeObject<Token>(content);
                    return token;
                }
                else
                {
                    throw new BigStashException("Server replied with success but response was empty.");
                }
            }
            catch (Exception e)
            {
                // If the caught exception is a BigStashException, then return it immediately
                // in order to be propagated to the higher caller as is, without wrapping it in
                // a new BigStashException instance.
                if (e is BigStashException)
                    throw;

                throw this.BigStashExceptionHandler(e);
            }
        }

        /// <summary>
        /// Get the user info sending a GET request.
        /// </summary>
        /// <returns></returns>
        public async Task<User> GetUserAsync()
        {
            _log.Debug("Called GetUserAsync.");

            var request = CreateHttpRequestWithSignature(GET, _userUri);
            HttpResponseMessage response;
                
            try
            {
                using (var httpClient = this.CreateHttpClientWithRetryLogic(LONGRETRY))
                {
                    response = await httpClient.SendAsync(request).ConfigureAwait(false);
                }

                //response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                JObject json = JObject.Parse(content);

                if (content != null)
                {
                    User user = JsonConvert.DeserializeObject<User>(content);
                    user.Archives = JsonConvert.DeserializeObject<IList<Archive>>(json["archives"]["results"].ToString());
                    return user;
                }
                else
                {
                    throw new Exceptions.BigStashException("Server replied with success but response was empty.");
                }
            }
            catch (Exception e)
            {
                // If the caught exception is a BigStashException, then return it immediately
                // in order to be propagated to the higher caller as is, without wrapping it in
                // a new BigStashException instance.
                if (e is BigStashException)
                    throw;

                throw this.BigStashExceptionHandler(e);
            }
        }

        /// <summary>
        /// Send a GET "archives/" request which returns all user's Deepfreeze archives.
        /// </summary>
        /// <returns>List of Archive</returns>
        public async Task<List<Archive>> GetArchivesAsync()
        {
            var request = CreateHttpRequestWithSignature(GET, _archivesUri);
            HttpResponseMessage response;

            try
            {
                using (var httpClient = this.CreateHttpClientWithRetryLogic(LONGRETRY))
                {
                    response = await httpClient.SendAsync(request).ConfigureAwait(false);
                }

                //response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                JObject json = JObject.Parse(content);

                if ((int)json["count"] > 0)
                {
                    var archives = JsonConvert.DeserializeObject<List<Archive>>(json["results"].ToString());
                    return archives;
                }
                else
                {
                    throw new Exceptions.BigStashException("Server replied with success but response was empty.");
                }
            }
            catch (Exception e)
            {
                // If the caught exception is a BigStashException, then return it immediately
                // in order to be propagated to the higher caller as is, without wrapping it in
                // a new BigStashException instance.
                if (e is BigStashException)
                    throw;

                throw this.BigStashExceptionHandler(e);
            }
        }

        /// <summary>
        /// Send a GET "archives/id" request which returns a user's Deepfreeze archive.
        /// </summary>
        /// <returns>List of Archive</returns>
        public async Task<Archive> GetArchiveAsync(string url)
        {
            _log.Debug("Called GetArchiveAsync with parameter url = \"" + url + "\".");

            var request = CreateHttpRequestWithSignature(GET, url, false);
            HttpResponseMessage response;

            try
            {
                using (var httpClient = this.CreateHttpClientWithRetryLogic(LONGRETRY))
                {
                    response = await httpClient.SendAsync(request).ConfigureAwait(false);
                }

                //response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (content != null)
                {
                    var archive = JsonConvert.DeserializeObject<Archive>(content);
                    return archive;
                }
                else
                {
                    throw new Exceptions.BigStashException("Server replied with success but response was empty.");
                }
            }
            catch (Exception e)
            {
                // If the caught exception is a BigStashException, then return it immediately
                // in order to be propagated to the higher caller as is, without wrapping it in
                // a new BigStashException instance.
                if (e is BigStashException)
                    throw;

                throw this.BigStashExceptionHandler(e);
            }
        }

        /// <summary>
        /// Send a POST "archives/" request which returns a Deepfreeze archive.
        /// This request is responsible for creating a new archive given a size and a title.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="title"></param>
        /// <returns>Archive</returns>
        public async Task<Archive> CreateArchiveAsync(long size, string title)
        {
            _log.Debug("Called CreateArchiveAsync with parameters size = '" + size + "' and title = \"" + title + "\".");

            ArchivePostData data = new ArchivePostData()
            {
                Size = size,
                Title = title
            };

            var request = CreateHttpRequestWithSignature(POST, _archivesUri);
            request.Content = new StringContent(data.ToJson(), Encoding.UTF8, "application/json");
            HttpResponseMessage response;

            try
            {
                using (var httpClient = this.CreateHttpClientWithRetryLogic(FASTRETRY))
                {
                    response = await httpClient.SendAsync(request).ConfigureAwait(false);
                }

                //response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (content != null)
                {
                    var archive = JsonConvert.DeserializeObject<Archive>(content);
                    return archive;
                }
                else
                    throw new Exceptions.BigStashException("Server replied with success but response was empty.");
            }
            catch (Exception e)
            {
                // If the caught exception is a BigStashException, then return it immediately
                // in order to be propagated to the higher caller as is, without wrapping it in
                // a new BigStashException instance.
                if (e is BigStashException)
                    throw;

                throw this.BigStashExceptionHandler(e);
            }
        }

        /// <summary>
        /// Send a GET "uploads/" request which returns all user's Deepfreeze uploads.
        /// </summary>
        /// <returns>List of Upload</returns>
        public async Task<List<Upload>> GetUploadsAsync()
        {
            var request = CreateHttpRequestWithSignature(GET, _uploadsUri);
            HttpResponseMessage response;

            try
            {
                using (var httpClient = this.CreateHttpClientWithRetryLogic(LONGRETRY))
                {
                    response = await httpClient.SendAsync(request).ConfigureAwait(false);
                }
                //response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                JObject json = JObject.Parse(content);

                if ((int)json["count"] > 0)
                {
                    var uploads = JsonConvert.DeserializeObject<List<Upload>>(json["results"].ToString());
                    return uploads;
                }
                else
                {
                    throw new Exceptions.BigStashException("Server replied with success but response was empty.");
                }
            }
            catch (Exception e)
            {
                // If the caught exception is a BigStashException, then return it immediately
                // in order to be propagated to the higher caller as is, without wrapping it in
                // a new BigStashException instance.
                if (e is BigStashException)
                    throw;

                throw this.BigStashExceptionHandler(e);
            }
        }

        /// <summary>
        /// Send a GET "uploads/id" request which returns a user's Deepfreeze upload.
        /// </summary>
        /// <returns>Upload</returns>
        public async Task<Upload> GetUploadAsync(string url, bool tryForever = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            _log.Debug("Called GetUploadAsync with parameter url = \"" + url + "\".");

            var request = CreateHttpRequestWithSignature(GET, url, false);
            HttpResponseMessage response;
            string content = String.Empty;

            try
            {
                var retries = LONGRETRY;

                // if tryForever is true, then set retries to the Int16 max value, that is 32767,
                // to ensure a large number of max retries are completed when transient errors occur.
                if (tryForever)
                {
                    retries = Int16.MaxValue;
                }

                using (var httpClient = this.CreateHttpClientWithRetryLogic(retries))
                {
                    response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }

                content = await response.Content.ReadAsStringAsync();

                if (content != null)
                {
                    var upload = JsonConvert.DeserializeObject<Upload>(content);
                    return upload;
                }
                else
                {
                    throw new Exceptions.BigStashException("Server replied with success but response was empty.");
                }
            }
            catch (Exception e)
            {
                // If the caught exception is a BigStashException, then return it immediately
                // in order to be propagated to the higher caller as is, without wrapping it in
                // a new BigStashException instance.
                if (e is BigStashException)
                    throw;

                throw this.BigStashExceptionHandler(e);
            }
        }

        /// <summary>
        /// Send a POST "Upload.Url"-url request which returns a Deepfreeze upload.
        /// This request is responsible for creating a new upload given an archive.
        /// </summary>
        /// <param name="archive"></param>
        /// <returns>Upload</returns>
        public async Task<Upload> InitiateUploadAsync(Archive archive)
        {
            _log.Debug("Called InitiateUploadAsync with parameter url = \"" + archive.UploadUrl + "\".");

            var request = CreateHttpRequestWithSignature(POST, archive.UploadUrl, false);
            HttpResponseMessage response;

            try
            {
                using (var httpClient = this.CreateHttpClientWithRetryLogic(FASTRETRY))
                {
                    response = await httpClient.SendAsync(request).ConfigureAwait(false);
                }

                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (content != null)
                {
                    var upload = JsonConvert.DeserializeObject<Upload>(content);
                    return upload;
                }
                else
                {
                    throw new Exceptions.BigStashException("Server replied with success but response was empty.");
                }
            }
            catch (Exception e)
            {
                // If the caught exception is a BigStashException, then return it immediately
                // in order to be propagated to the higher caller as is, without wrapping it in
                // a new BigStashException instance.
                if (e is BigStashException)
                    throw;

                throw this.BigStashExceptionHandler(e);
            }
        }

        /// <summary>
        /// Send a PATCH "Upload.Url"-url request which returns a Deepfreeze upload.
        /// This request is responsible for patching an upload resource with the specified patchContent parameter.
        /// </summary>
        /// <param name="upload"></param>
        /// <param name="patchContent"></param>
        /// <returns></returns>
        public async Task<Upload> PatchUploadAsync(Upload upload, string patchContent)
        {
            _log.Debug("Called PatchUploadAsync with parameters url = \"" + upload.Url + "\" and content = \"" + patchContent + "\".");

            var request = CreateHttpRequestWithSignature(PATCH, upload.Url, false);
            request.Content = new StringContent(patchContent, Encoding.UTF8, "application/json");
            HttpResponseMessage response;

            try
            {
                using (var httpClient = this.CreateHttpClientWithRetryLogic(LONGRETRY))
                {
                    response = await httpClient.SendAsync(request).ConfigureAwait(false);
                }

                //response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (content != null)
                {
                    var patchedUpload = JsonConvert.DeserializeObject<Upload>(content);
                    return patchedUpload;
                }
                else
                {
                    throw new Exceptions.BigStashException("Server replied with success but response was empty.");
                }
            }
            catch (Exception e)
            {
                // If the caught exception is a BigStashException, then return it immediately
                // in order to be propagated to the higher caller as is, without wrapping it in
                // a new BigStashException instance.
                if (e is BigStashException)
                    throw;

                throw this.BigStashExceptionHandler(e);
            }
        }

        /// <summary>
        /// Send a PATCH request with status = uploaded to mark this upload as finished from the client's perspective.
        /// </summary>
        /// <param name="upload"></param>
        /// <returns></returns>
        public async Task<Upload> FinishUploadAsync(Upload upload)
        {
            _log.Debug("Called FinishUploadAsync with parameter url = \"" + upload.Url + "\".");

            try
            {
                var patchedUpload = await this.PatchUploadAsync(upload, @"{""status"": ""uploaded""}").ConfigureAwait(false);

                return patchedUpload;
            }
            catch (Exception)
            { throw; }
        }

        /// <summary>
        /// Send a DELETE "Upload.Url"-url request which deletes a Deepfreeze upload.
        /// </summary>
        /// <param name="archive"></param>
        /// <returns>bool</returns>
        public async Task<bool> DeleteUploadAsync(Upload upload)
        {
            _log.Debug("Called DeleteUploadAsync with parameter url = \"" + upload.Url + "\".");

            var request = CreateHttpRequestWithSignature(DELETE, upload.Url, false);
            HttpResponseMessage response;

            try
            {
                using (var httpClient = this.CreateHttpClientWithRetryLogic(FASTRETRY))
                {
                    response = await httpClient.SendAsync(request).ConfigureAwait(false);
                }

                //response.EnsureSuccessStatusCode();

                return true;
            }
            catch (Exception e)
            {
                // If the caught exception is a BigStashException, then return it immediately
                // in order to be propagated to the higher caller as is, without wrapping it in
                // a new BigStashException instance.
                if (e is BigStashException)
                    throw;

                throw this.BigStashExceptionHandler(e);
            }
        }

        /// <summary>
        /// Send a GET "notifications/" request with an optional page parameter which returns an enumerable of user's BigStash Notification objects.
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<Notification>> GetNotificationsAsync(string url = null, bool tryForever = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            _log.Debug("Called GetNotificationsAsync.");

            var request = String.IsNullOrEmpty(url) ? CreateHttpRequestWithSignature(GET, _notificationsUri)
                                                    : CreateHttpRequestWithSignature(GET, url, false);
            HttpResponseMessage response;
            string content = String.Empty;

            try
            {
                var retries = LONGRETRY;

                if (tryForever)
                {
                    retries = Int16.MaxValue;
                }

                using(var httpClient = this.CreateHttpClientWithRetryLogic(retries))
                {
                    response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }

                content = await response.Content.ReadAsStringAsync();

                JObject json = JObject.Parse(content);

                if ((int)json["meta"]["count"] > 0)
                {
                    var notifications = JsonConvert.DeserializeObject<IEnumerable<Notification>>(json["results"].ToString());

                    return notifications;
                }
                else
                {
                    throw new Exceptions.BigStashException("Server replied with success but response was empty.");
                }
            }
            catch(Exception e)
            {
                // If the caught exception is a BigStashException, then return it immediately
                // in order to be propagated to the higher caller as is, without wrapping it in
                // a new BigStashException instance.
                if (e is BigStashException)
                    throw;

                throw this.BigStashExceptionHandler(e);
            }
        }

        /// <summary>
        /// Send a GET "notifications/id" request which returns a user's BigStash Notification object.
        /// </summary>
        /// <returns></returns>
        public async Task<Notification> GetNotificationAsync(string id, bool tryForever = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            _log.Debug("Called GetNotificationAsync with parameter id = \"" + id + "\".");

            var url = _notificationsUri + id + "/";
            var request = CreateHttpRequestWithSignature(GET, url, false);
            HttpResponseMessage response;
            string content = String.Empty;

            try
            {
                var retries = LONGRETRY;

                // if tryForever is true, then set retries to the Int16 max value, that is 32767,
                // to ensure a large number of max retries are completed when transient errors occur.
                if (tryForever)
                {
                    retries = Int16.MaxValue;
                }

                using (var httpClient = this.CreateHttpClientWithRetryLogic(retries))
                {
                    response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }

                content = await response.Content.ReadAsStringAsync();

                if (content != null)
                {
                    var notification = JsonConvert.DeserializeObject<Notification>(content);
                    return notification;
                }
                else
                {
                    throw new Exceptions.BigStashException("Server replied with success but response was empty.");
                }
            }
            catch (Exception e)
            {
                // If the caught exception is a BigStashException, then return it immediately
                // in order to be propagated to the higher caller as is, without wrapping it in
                // a new BigStashException instance.
                if (e is BigStashException)
                    throw;

                throw this.BigStashExceptionHandler(e);
            }
        }

        #endregion

        #region private methods

        /// <summary>
        /// Create an instance of HttpClient implementing retry logic by using the RetryDelegatingHanlder DelegatingHandler.
        /// </summary>
        /// <returns></returns>
        private HttpClient CreateHttpClientWithRetryLogic(int retry)
        {
            return new HttpClient(new RetryDelegatingHanlder(new HttpClientHandler(), retry), true);
        }

        /// <summary>
        /// Handle and create exceptions occuring while trying requests to the BigStash API.
        /// </summary>
        /// <param name="innerException"></param>
        /// <param name="memberName"></param>
        /// <returns></returns>
        private BigStashException BigStashExceptionHandler(Exception innerException,
                                      [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            StringBuilder errorMsg = new StringBuilder();
            errorMsg.Append(memberName);
            errorMsg.Append(" threw exception:");

            _log.Error(errorMsg.ToString(), innerException);

            var errorType = DeepfreezeSDK.Exceptions.ErrorType.NotSet;

            // If the InnerException is thrown by HttpClient.SendAsync() then
            // the error type should be ErrorType.Service.
            if (innerException is HttpRequestException)
            {
                errorType = Exceptions.ErrorType.Service;
            }
            else
            {
                errorType = Exceptions.ErrorType.Client;
            }

            return new BigStashException(memberName, innerException, errorType);
        }

        /// <summary>
        /// Get the connected state of the OS running this client.
        /// </summary>
        /// <param name="Description"></param>
        /// <param name="ReservedValue"></param>
        /// <returns></returns>
        [DllImport("wininet.dll")]
        private extern static bool InternetGetConnectedState(out int Description, int ReservedValue);

        /// <summary>
        /// Create a HttpRequestMessage with Http-Signature authorization
        /// and other HTTP headers as defined in the Deepfreeze API,
        /// given a method name and a uri resource or complete url. The optional
        /// isRelative parameter defines if a relative uri is given or a complete url.
        /// Send the request using httpClient.SendAsync(request) method. If a content 
        /// is needed, then costruct it in request.Content before sending it.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="resource"></param>
        /// <param name="isRelative"></param>
        /// <returns>HttpRequestMessage</returns>
        private HttpRequestMessage CreateHttpRequestWithSignature(string method, string resource, bool isRelative = true)
        {
            DateTimeOffset date = DateTime.Now;

            // create a new Http request message.
            HttpRequestMessage message = new HttpRequestMessage();
            message.Method = new HttpMethod(method);
            
            // if resource is relative, construct the request url.
            if (isRelative)
            {
                message.RequestUri = new UriBuilder(this.Settings.ApiEndpoint + resource).Uri;
            }
            // else use only the resource variable since it already has the url value
            else
            {
                message.RequestUri = new UriBuilder(resource).Uri;
            }
            
            // set use agent header
            // TODO: should reflect the production version along with some platform information.
            message.Headers.UserAgent.Add(new ProductInfoHeaderValue("bigstash-windows-desktop", this.ApplicationVersion));

            // set host header
            message.Headers.Host = message.RequestUri.Authority;
            // set accept header
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.deepfreeze+json"));

            message.Headers.Add("X-Deepfreeze-Api-Key", this.Settings.ActiveToken.Key);

            // set date header
            message.Headers.Date = date;

            var signature = CreateHMACSHA256Signature(method, message.RequestUri, date, isRelative);

            // add authorization header
            message.Headers.Authorization = new AuthenticationHeaderValue("Signature", AUTHORIZATION + @",signature=""" + signature + @"""");

            return message;
        }

        /// <summary>
        /// Construct the text to sign and use HMACSHA256Sign method to return
        /// the HMACSHA256 signature.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="resource"></param>
        /// <param name="date"></param>
        /// <param name="isRelative"></param>
        /// <returns>string</returns>
        private string CreateHMACSHA256Signature(string method, Uri requestUri, DateTimeOffset date, bool isRelative = true)
        {
            // create string as source for the signature.
            // use \n for LF (Unix format)
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("(request-line): {0} ", method.ToLower());

            sb.Append(String.Join("", requestUri.Segments));
            sb.Append("\n");

            sb.AppendFormat("host: {0}\n", requestUri.Authority);
            sb.AppendFormat("accept: {0}\n", ACCEPT);
            sb.AppendFormat("date: {0}", date.ToUniversalTime().ToString("r"));

            // get signature
            string signature = HelperMethods.HMACSHA256Sign(this.Settings.ActiveToken.Secret, sb.ToString());

            return signature;
        }

        #endregion
    }
}
