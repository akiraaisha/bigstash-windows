using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Net.Http;
using System.Net.Http.Headers;

using DeepfreezeModel;

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System.IO;
using Amazon.Runtime;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;



namespace DeepfreezeSDK
{
    [Export(typeof(IDeepfreezeClient))]
    public class DeepfreezeClient
    {
        #region fields

        // strings representing http methods names in lower case;
        private readonly string GET = "GET";
        private readonly string POST = "POST";
        private readonly string PUT = "PUT";
        private readonly string PATCH = "PATCH";
        private readonly string DELETE = "DELETE";

        // Currently pointing to beta stage api.
        private readonly string HOST = "stage.deepfreeze.io";
        private readonly string ACCEPT = "application/vnd.deepfreeze+json";
        private readonly string AUTHORIZATION = @"keyId=""hmac-key-1"",algorithm=""hmac-sha256"",headers=""(request-line) host accept date""";

        private Token _TOKEN;

        private readonly string _baseEndPoint = "https://stage.deepfreeze.io";
        private readonly string _apiEndPoint = "/api/v1";
        private readonly string _tokenUri = "/tokens/";
        private readonly string _userUri = "/user/";
        private readonly string _uploadsUri = "/uploads/";
        private readonly string _archivesUri = "/archives/";
        
        #endregion

        #region methods

        /// <summary>
        /// Return all active Deepfreeze authorization Tokens for the authorized user.
        /// </summary>
        /// <returns>Token</returns>
        public async Task<List<Token>> GetTokensAsync()
        {
            try
            {
                using(var httpClient = new HttpClient())
                {
                    var request = CreateHttpRequestWithSignature(GET, _tokenUri);
                    var response = await httpClient.SendAsync(request);

                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(content);

                    if ((int)json["count"] > 0)
                    {
                        var tokens = JsonConvert.DeserializeObject<List<Token>>(json["results"].ToString());
                        return tokens;
                    }
                    else
                    {
                        throw new Exceptions.NoActiveTokenException();
                    }
                }
            }
            catch(AggregateException e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Create a Deepfreeze Token.
        /// </summary>
        /// <returns>TokenPostResponse</returns>
        public async Task<Token> CreateTokenAsync(string authorizationString)
        {
            try
            {
                // Only for this request, the client uses basic auth with user credentials.
                // For every other authorized actions, a signed request should be sent.
                using(var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.deepfreeze+json"));
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authorizationString);

                    var requestUri = new UriBuilder(_baseEndPoint + _apiEndPoint + _tokenUri).Uri;
                    var response = await httpClient.PostAsync(requestUri, null);

                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();

                    if (content != null)
                    {
                        var tokenResponse = JsonConvert.DeserializeObject<Token>(content);
                        return tokenResponse;
                    }
                    else
                    {
                        throw new Exceptions.CreateTokenException();
                    }
                }
            }
            catch (AggregateException e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Login user using a Deepfreeze Token.
        /// </summary>
        /// <param name="authorizationToken"></param>
        /// <returns>Boolean</returns>
        public async Task<bool> LoginAsync(Token authorizationToken)
        {
            try
            {
                var tokens = await this.GetTokensAsync();

                return tokens.Select(x => x.Key).Contains(authorizationToken.Key);
            }
            catch(AggregateException e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Login user using account credentials and create a Deepfreeze Token.
        /// This method also validates the new Token.
        /// </summary>
        /// <param name="authorizationString"></param>
        /// <returns></returns>
        public async Task<bool> LoginAsync(string authorizationString)
        {
            try
            {
                var tokenResponse = await this.CreateTokenAsync(authorizationString);

                var tokens = await this.GetTokensAsync();

                return tokens.Select(x => x.Key).Contains(tokenResponse.Key);
            }
            catch (AggregateException e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Get the user info sending a GET request.
        /// </summary>
        /// <returns></returns>
        public async Task<User> GetUserAsync()
        {
            try
            {
                var request = CreateHttpRequestWithSignature(GET, _userUri);

                using(var httpClient = new HttpClient())
                {
                    var response = await httpClient.SendAsync(request);

                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(content);

                    User user = new User()
                    {
                        ID = (int)json["id"],
                        Email = (string)json["email"],
                        DateJoined = (DateTime)json["date_joined"],
                        DisplayName = (string)json["displayname"]
                    };

                    return user;
                }
            }
            catch(AggregateException e)
            { throw e; }
        }

        /// <summary>
        /// Send a GET "archives/" request which returns all user's Deepfreeze archives.
        /// </summary>
        /// <returns>List of Archive</returns>
        public async Task<List<Archive>> GetArchivesAsync()
        {
            try
            {
                using(var httpClient = new HttpClient())
                {
                    var request = CreateHttpRequestWithSignature(GET, _archivesUri);
                    var response = await httpClient.SendAsync(request);

                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(content);

                    if ((int)json["count"] > 0)
                    {
                        var archives = JsonConvert.DeserializeObject<List<Archive>>(json["results"].ToString());
                        return archives;
                    }
                    else
                    {
                        throw new Exceptions.NoArchivesFoundException();
                    }
                }
            }
            catch(AggregateException e)
            { throw e; }
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
            try
            {
                ArchivePostData data = new ArchivePostData()
                {
                    Size = size,
                    Title = title
                };

                var request = CreateHttpRequestWithSignature(POST, _archivesUri);
                request.Content = new StringContent(data.ToJson(), Encoding.ASCII, "application/json");

                using(var httpClient = new HttpClient())
                {
                    var response = await httpClient.SendAsync(request);

                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();

                    if (content != null)
                    {
                        var archive = JsonConvert.DeserializeObject<Archive>(content);
                        return archive;
                    }
                    else
                        throw new Exceptions.CreateArchiveException();
                }
            }
            catch(AggregateException e)
            { throw e; }
        }

        /// <summary>
        /// Send a GET "uploads/" request which returns all user's Deepfreeze uploads.
        /// </summary>
        /// <returns>List of Upload</returns>
        public async Task<List<Upload>> GetUploadsAsync()
        {
            try
            {
                var request = CreateHttpRequestWithSignature(GET, _uploadsUri);

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.SendAsync(request);

                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(content);

                    if ((int)json["count"] > 0)
                    {
                        var uploads = JsonConvert.DeserializeObject<List<Upload>>(json["results"].ToString());
                        return uploads;
                    }
                    else
                    {
                        throw new Exceptions.NoUploadsFoundException();
                    }
                }
            }
            catch (AggregateException e)
            { throw e; }
        }

        /// <summary>
        /// Send a POST "Upload.Url"-url request which returns a Deepfreeze upload.
        /// This request is responsible for creating a new upload given an archive.
        /// </summary>
        /// <param name="archive"></param>
        /// <returns>Upload</returns>
        public async Task<Upload> CreateUploadAsync(Archive archive)
        {
            try
            {
                var request = CreateHttpRequestWithSignature(POST, archive.UploadUrl, false);

                using(var httpClient = new HttpClient())
                {
                    var response = await httpClient.SendAsync(request);

                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();

                    if (content != null)
                    {
                        var upload = JsonConvert.DeserializeObject<Upload>(content);
                        return upload;
                    }
                    else
                        throw new Exceptions.CreateUploadException();
                }
            }
            catch(AggregateException e)
            { throw e; }
        }


        public async Task<Upload> FinishUploadAsync(Upload upload)
        {
            try
            {
                var request = CreateHttpRequestWithSignature(PATCH, upload.Url, false);
                request.Content = new StringContent(@"{""status"": ""uploaded""}", Encoding.ASCII, "application/json");

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.SendAsync(request);

                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();

                    if (content != null)
                    {
                        var patchedUpload = JsonConvert.DeserializeObject<Upload>(content);
                        return patchedUpload;
                    }
                    else
                        throw new Exceptions.CreateUploadException();
                }
            }
            catch(AggregateException e)
            { throw e; }
        }

        /// <summary>
        /// Send a DELETE "Upload.Url"-url request which deletes a Deepfreeze upload.
        /// </summary>
        /// <param name="archive"></param>
        /// <returns>bool</returns>
        public async Task<bool> DeleteUploadAsync(Upload upload)
        {
            try
            {
                var request = CreateHttpRequestWithSignature(DELETE, upload.Url, false);

                using (var httpClient = new HttpClient())
                {
                    //var response = await httpClient.DeleteAsync(upload.Url);
                    var response = await httpClient.SendAsync(request);

                    response.EnsureSuccessStatusCode();

                    // debug
                    //var content = response.Content.ReadAsStringAsync();

                    return true;
                }
            }
            catch (AggregateException e)
            { throw e; }
        }

        public Archive PrepareArchive(string title, IList<ArchiveFileInfo> files)
        {
            var archive = new Archive();
            archive.Title = title;
            // TODO: consume DF API 
            // archive.ID = 
            archive.Size = 0;
            foreach (var file in files)
            {
                archive.Size += file.Size;
            }
            
            string json = archive.ToJson();
            return archive;
        }

        #endregion

        #region private methods

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
                message.RequestUri = new UriBuilder(_baseEndPoint + _apiEndPoint + resource).Uri;
            }
            // else use only the resource variable since it already has the url value
            else
            {
                message.RequestUri = new UriBuilder(resource).Uri;
            }
            
            // set use agent header
            // TODO: should reflect the production version along with some platform information.
            message.Headers.UserAgent.Add(new ProductInfoHeaderValue("deepfreeze-windows-desktop", "alpha"));

            // set host header
            message.Headers.Host = HOST;
            // set accept header
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.deepfreeze+json"));

            message.Headers.Add("X-Deepfreeze-Api-Key", _TOKEN.Key);

            // set date header
            message.Headers.Date = date;

            var signature = CreateHMACSHA256Signature(method, resource, date, isRelative);

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
        private string CreateHMACSHA256Signature(string method, string resource, DateTimeOffset date, bool isRelative = true)
        {
            // create string as source for the signature.
            // use \n for LF (Unix format)
            StringBuilder sb = new StringBuilder();

            // if resource is relative, construct the request url.
            if (isRelative)
            {
                sb.AppendFormat("(request-line): {0} {1}{2}\n", method.ToLower(), _apiEndPoint, resource);
            }
            // else use only the resource variable since it already has the url value
            else
            {
                sb.AppendFormat("(request-line): {0} {1}\n", method.ToLower(), resource.Replace(_baseEndPoint, ""));
            }

            sb.AppendFormat("host: {0}\n", HOST);
            sb.AppendFormat("accept: {0}\n", ACCEPT);
            //sb.AppendFormat("x-deepfreeze-api-key: {0}\n", token); 
            sb.AppendFormat("date: {0}", date.ToUniversalTime().ToString("r"));

            // get signature
            string signature = HelperMethods.HMACSHA256Sign(_TOKEN.Secret, sb.ToString());

            return signature;
        }

        #endregion

        #region constructor & public setters/getters

        public DeepfreezeClient() { }

        [ImportingConstructor]
        public DeepfreezeClient(Token token)
        {
            this.SetClientToken(token);
        }

        public void SetClientToken(Token token)
        {
            this._TOKEN = token;
        }

        #endregion
    }
}
