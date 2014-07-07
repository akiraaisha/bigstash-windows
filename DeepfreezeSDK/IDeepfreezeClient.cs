﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Amazon.S3.Model;
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
        Task<Upload> GetUploadAsync(string url);

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

        //void Setup(string accessKeyID, string secretAccessKey);

        //Task<List<S3Bucket>> ListBucketsAsync(CancellationToken token);

        //Task<List<S3Object>> ListObjectsByBucketAsync(string bucketName);

        //Task<List<S3Object>> ListObjectsByBucketAsync(string bucketName, CancellationToken token);

        //Task UploadFileAsync(string existingBucketName, string keyName, string filePath, CancellationToken cts);

        //Task<List<MultipartUpload>> ListMultiPartUploadsAsync(string existingBucketName);

        //Task AbortMultiPartUploadAsync(string existingBucketName, string keyName, string uploadID, CancellationToken cts);
    }
}
