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

        // Currently pointing to beta stage api.
        private readonly string _baseEndPoint = "https://stage.deepfreeze.io/";
        private readonly string _apiEndPoint = "api/v1/";
        private readonly string _tokenUri = "tokens/";
        private readonly string _userUri = "user/";
        private readonly string _uploadsUri = "uploads/";
        private readonly string _archivesUri = "archives/";
        private HttpClient _httpClient;
        
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
                var response = await this._httpClient.GetAsync(_tokenUri);

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
            catch(AggregateException e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Create a Deepfreeze Token.
        /// </summary>
        /// <returns>TokenPostResponse</returns>
        public async Task<TokenPostResponse> CreateTokenAsync()
        {
            try
            {
                var response = await this._httpClient.PostAsync(_tokenUri, null);

                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();

                if (content != null)
                {
                    var tokenResponse = JsonConvert.DeserializeObject<TokenPostResponse>(content);
                    return tokenResponse;
                }
                else
                {
                    throw new Exceptions.CreateTokenException();
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
                this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", authorizationToken.Key);

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
                this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authorizationString);

                var tokenResponse = await this.CreateTokenAsync();

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
                var response = await this._httpClient.GetAsync(_userUri);

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
                var response = await this._httpClient.GetAsync(_archivesUri);

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

                var httpContent = new StringContent(data.ToJson(), Encoding.UTF8, "application/json");
                
                var response = await this._httpClient.PostAsync(_archivesUri, httpContent);

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
                var response = await this._httpClient.GetAsync(_uploadsUri);

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
            catch (AggregateException e)
            { throw e; }
        }

        /// <summary>
        /// Send a POST "Archive.Upload"-url request which returns a Deepfreeze upload.
        /// This request is responsible for creating a new upload given an archive.
        /// </summary>
        /// <param name="archive"></param>
        /// <returns>Upload</returns>
        public async Task<Upload> CreateUploadAsync(Archive archive)
        {
            try
            {
                var uri = archive.UploadUrl.Replace(this._httpClient.BaseAddress.ToString(), ""); //.Replace("http", "https");

                var response = await this._httpClient.PostAsync(uri, null);

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
                var uri = upload.Url.Replace(this._httpClient.BaseAddress.ToString(), "");//.Replace("http", "https");

                var response = await this._httpClient.DeleteAsync(uri);

                response.EnsureSuccessStatusCode();

                return true;
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

        #region constructor & initialization

        public DeepfreezeClient()
        {
            this._httpClient = InititializeHttpClient();
        }

        public DeepfreezeClient(string authorization)
        {
            this._httpClient = InititializeHttpClient();
            this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authorization);
        }

        private HttpClient InititializeHttpClient()
        {
            var httpClient = new HttpClient();
            var uriBuilder = new UriBuilder(_baseEndPoint + _apiEndPoint);
            httpClient.BaseAddress = uriBuilder.Uri;
            // Add Accept Header with special deepfreeze mimetype.
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.deepfreeze+json"));
            return httpClient;
        }

        #endregion
    }
}
