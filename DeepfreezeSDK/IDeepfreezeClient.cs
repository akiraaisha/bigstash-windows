using System;
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

        //void Setup(string accessKeyID, string secretAccessKey);

        //Task<List<S3Bucket>> ListBucketsAsync(CancellationToken token);

        //Task<List<S3Object>> ListObjectsByBucketAsync(string bucketName);

        //Task<List<S3Object>> ListObjectsByBucketAsync(string bucketName, CancellationToken token);

        //Task UploadFileAsync(string existingBucketName, string keyName, string filePath, CancellationToken cts);

        //Task<List<MultipartUpload>> ListMultiPartUploadsAsync(string existingBucketName);

        //Task AbortMultiPartUploadAsync(string existingBucketName, string keyName, string uploadID, CancellationToken cts);
    }
}
