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
        /// Send a POST "archives/" request which returns a Deepfreeze archive.
        /// This request is responsible for creating a new archive given a size and a title.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="title"></param>
        /// <returns>Archive</returns>
        public async Task<Archive> PostNewArchiveAsync(long size, string title)
        {
            try
            {
                ArchivePostData data = new ArchivePostData()
                {
                    Size = size,
                    Title = title
                };

                var response = await this._httpClient.PostAsync(_archivesUri, new StringContent(data.ToJson()));

                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                JObject json = JObject.Parse(content);

                Archive archive = new Archive()
                {
                    Status = Enumerations.GetStatusFromString((string)json["status"]),
                    Size = (long)json["size"],
                    Key = (string)json["key"],
                    Checksum = (string)json["checksum"],
                    Created = (DateTime)json["created"],
                    Title = (string)json["title"],
                    Url = (string)json["url"]
                };

                return archive;
            }
            catch(AggregateException e)
            { throw e; }
        }

        /// <summary>
        /// Send a POST "archiveurl" request which returns a Deepfreeze upload.
        /// This request is responsible for creating a new upload given an archive.
        /// </summary>
        /// <param name="archive"></param>
        /// <returns>Upload</returns>
        public async Task<Upload> PostArchiveUrlAsync(Archive archive)
        {
            try
            {
                var id = archive.Key.Split('-').FirstOrDefault();
                var response = await this._httpClient.PostAsync(_archivesUri + id, null);

                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                JObject json = JObject.Parse(content);

                Upload upload = new Upload()
                {
                    Url = (string)json["url"],
                    ArchiveUrl = (string)json["archive"],
                    Created = (DateTime)json["created"],
                    Status = Enumerations.GetStatusFromString((string)json["status"]),
                    Comment = (string)json["comment"]
                };

                return upload;
            }
            catch(AggregateException e)
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
            archive.Files = files;
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
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.deepfreeze+json; indent=4"));
            return httpClient;
        }

        #endregion
    }
}
