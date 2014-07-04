using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeepfreezeSDK
{
    public interface IDeepfreezeClient
    {
        //void Setup(string accessKeyID, string secretAccessKey);

        //Task<List<S3Bucket>> ListBucketsAsync(CancellationToken token);

        //Task<List<S3Object>> ListObjectsByBucketAsync(string bucketName);

        //Task<List<S3Object>> ListObjectsByBucketAsync(string bucketName, CancellationToken token);

        //Task UploadFileAsync(string existingBucketName, string keyName, string filePath, CancellationToken cts);

        //Task<List<MultipartUpload>> ListMultiPartUploadsAsync(string existingBucketName);

        //Task AbortMultiPartUploadAsync(string existingBucketName, string keyName, string uploadID, CancellationToken cts);
    }
}
