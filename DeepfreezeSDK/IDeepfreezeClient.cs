using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using DeepfreezeModel;

namespace DeepfreezeSDK
{
    public interface IDeepfreezeClient
    {
        Settings Settings { get; set; }

        /// <summary>
        /// Check if DeepfreezeClient has a Settings property instatiated,
        /// and if the ActiveUser and ActiveToken are set. Return true if all stand.
        /// </summary>
        /// <returns>bool</returns>
        bool IsLogged();

        bool IsInternetConnected { get; }

        string ApplicationVersion { get; set; }

        /// <summary>
        /// Create a new Deepfreeze API token using user credentials.
        /// </summary>
        /// <param name="authorizationString"></param>
        /// <returns>Token</returns>
        Task<Token> CreateTokenAsync(string authorizationString);

        /// <summary>
        /// Get the Deepfreeze User.
        /// </summary>
        /// <returns></returns>
        Task<User> GetUserAsync();

        /// <summary>
        /// Send a GET "archives/id" request which returns a user's Deepfreeze archive.
        /// </summary>
        /// <returns>List of Archive</returns>
        Task<Archive> GetArchiveAsync(string url);

        /// <summary>
        /// Send a POST "archives/" request which returns a Deepfreeze archive.
        /// This request is responsible for creating a new archive given a size and a title.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="title"></param>
        /// <returns>Archive</returns>
        Task<Archive> CreateArchiveAsync(long size, string title);

        /// <summary>
        /// Send a GET "uploads/id" request which returns a user's Deepfreeze upload.
        /// </summary>
        /// <returns>Upload</returns>
        Task<Upload> GetUploadAsync(string url, bool tryForever = false);

        /// <summary>
        /// Send a POST "Upload.Url"-url request which returns a Deepfreeze upload.
        /// This request is responsible for creating a new upload given an archive.
        /// </summary>
        /// <param name="archive"></param>
        /// <returns>Upload</returns>
        Task<Upload> InitiateUploadAsync(Archive archive);

        /// <summary>
        /// Send a DELETE "Upload.Url"-url request which deletes a Deepfreeze upload.
        /// </summary>
        /// <param name="archive"></param>
        /// <returns>bool</returns>
        Task<bool> DeleteUploadAsync(Upload upload);

        /// <summary>
        /// Send a PATCH request with status = uploaded to mark this upload as finished from the client's perspective.
        /// </summary>
        /// <param name="upload"></param>
        /// <returns></returns>
        Task<Upload> FinishUploadAsync(Upload upload);
    }
}
