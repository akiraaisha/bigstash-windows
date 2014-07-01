using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using DeepfreezeModel;

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System.IO;
using Amazon.Runtime;

namespace DeepfreezeSDK
{
    [Export(typeof(IDeepfreezeS3Client))]
    public class DeepfreezeS3Client : IDeepfreezeS3Client
    {
        protected static readonly long PART_SIZE = 1024 * 1024;

        static IAmazonS3 s3Client;

        private static AmazonS3Config _s3Config;
        private static string _accessKeyID;
        private static string _secretAccessKey;

        public DeepfreezeS3Client()
        { }

        /// <summary>
        /// Setup the S3 Client with the user credentials and the correct S3 Configuation.
        /// </summary>
        /// <param name="accessKeyID"></param>
        /// <param name="secretAccessKey"></param>
        public void Setup(string accessKeyID, string secretAccessKey)
        {
            _s3Config = new AmazonS3Config() { RegionEndpoint = Amazon.RegionEndpoint.EUWest1 };
            s3Client = AWSClientFactory.CreateAmazonS3Client(accessKeyID, secretAccessKey, _s3Config);
            _accessKeyID = accessKeyID;
            _secretAccessKey = secretAccessKey;
        }

        /// <summary>
        /// Lists buckets.
        /// </summary>
        public async Task<List<S3Bucket>> ListBucketsAsync(CancellationToken token)
        {
            try
            {
                ListBucketsResponse response = await s3Client.ListBucketsAsync(new ListBucketsRequest(), token);

                return response.Buckets;
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null &&
                    (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId") ||
                    amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Console.WriteLine("Please check the provided AWS Credentials.");
                    Console.WriteLine("If you haven't signed up for Amazon S3, please visit http://aws.amazon.com/s3");
                }
                else
                {
                    Console.WriteLine("An Error, number {0}, occurred when listing buckets with the message '{1}", amazonS3Exception.ErrorCode, amazonS3Exception.Message);
                }

                throw amazonS3Exception;
            }
        }

        public async Task<List<S3Object>> ListObjectsByBucketAsync(string bucketName)
        {
            return await ListObjectsByBucketAsync(bucketName, CancellationToken.None);
        }
        public async Task<List<S3Object>> ListObjectsByBucketAsync(string bucketName, CancellationToken token)
        {
            try
            {
                ListObjectsRequest request = new ListObjectsRequest();
                request.BucketName = bucketName;
                ListObjectsResponse response = await s3Client.ListObjectsAsync(request, token);

                return response.S3Objects;
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null && (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId") || amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Console.WriteLine("Please check the provided AWS Credentials.");
                    Console.WriteLine("If you haven't signed up for Amazon S3, please visit http://aws.amazon.com/s3");
                }
                else
                {
                    Console.WriteLine("An error occurred with the message '{0}' when listing objects", amazonS3Exception.Message);
                }

                throw amazonS3Exception;
            }
        }

        public void ReadingAnObject(string bucketName, string keyName)
        {
            try
            {
                GetObjectRequest request = new GetObjectRequest()
                {
                    BucketName = bucketName,
                    Key = keyName
                };

                using (GetObjectResponse response = s3Client.GetObject(request))
                {
                    string title = response.Metadata["x-amz-meta-title"];
                    Console.WriteLine("The object's title is {0}", title);
                    string dest = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), keyName);
                    if (!System.IO.File.Exists(dest))
                    {
                        response.WriteResponseStreamToFile(dest);
                    }
                }
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null &&
                    (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId") ||
                    amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Console.WriteLine("Please check the provided AWS Credentials.");
                    Console.WriteLine("If you haven't signed up for Amazon S3, please visit http://aws.amazon.com/s3");
                }
                else
                {
                    Console.WriteLine("An error occurred with the message '{0}' when reading an object", amazonS3Exception.Message);
                }
            }
        }

        /// <summary>
        /// Returns an InitiateMultipartUploadRequest to an existing Bucket for a new object with key = keyName.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="keyName"></param>
        /// <returns></returns>
        public InitiateMultipartUploadRequest CreateInitiateMutlipartUploadRequest(string existingBucketName, string keyName)
        {
            InitiateMultipartUploadRequest initiateRequest = new InitiateMultipartUploadRequest
            {
                BucketName = existingBucketName,
                Key = keyName
            };

            return initiateRequest;
        }

        /// <summary>
        /// Create an UploadPartRequest.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="keyName"></param>
        /// <param name="uploadID"></param>
        /// <param name="partNumber"></param>
        /// <param name="partSize"></param>
        /// <param name="filePosition"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public UploadPartRequest CreateUploadPartRequest(string existingBucketName, string keyName, string uploadID,
                                                         int partNumber, long partSize, long filePosition, string filePath)
        {
            // Create request to upload a part.
            UploadPartRequest uploadRequest = new UploadPartRequest
            {
                BucketName = existingBucketName,
                Key = keyName,
                UploadId = uploadID,
                PartNumber = partNumber,
                PartSize = partSize,
                FilePosition = filePosition,
                FilePath = filePath
            };

            return uploadRequest;
        }

        public async Task<UploadPartResponse> BeginPartUploadAsync(string existingBucketName, string keyName, string uploadID,
                                                         int partNumber, long partSize, long filePosition, string filePath,
                                                         CancellationToken token)
        {
            try
            {
                // Create request to upload a part.
                var uploadPartRequest = CreateUploadPartRequest(existingBucketName, keyName, uploadID, partNumber, partSize, filePosition, filePath);

                // Upload part and add response to our list.
                uploadPartRequest.StreamTransferProgress +=
                    new EventHandler<StreamTransferProgressArgs>(UploadPartProgressEventCallback);

                var uploadPartResponse = await s3Client.UploadPartAsync(uploadPartRequest, token);

                return uploadPartResponse;
            }
            catch(Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Returns a CompleteMultipartUploadRequest with set properties for Bucket name, Key name and UploadID.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="keyName"></param>
        /// <param name="uploadID"></param>
        /// <returns></returns>
        public CompleteMultipartUploadRequest CreateCompleteMultipartUploadRequest(string existingBucketName, string keyName, string uploadID)
        {
            CompleteMultipartUploadRequest completeRequest = new CompleteMultipartUploadRequest
            {
                BucketName = existingBucketName,
                Key = keyName,
                UploadId = uploadID,
            };

            return completeRequest;
        }

        

        public async Task<UploadOperation> CreateUploadOperation(Archive archive, string existingBucketName)
        {
            var upload = new UploadOperation();

            upload.BucketName = existingBucketName;
            upload.CreationDate = DateTime.Now;
            upload.PartSize = PART_SIZE;
            upload.Status = Enumerations.Status.Pending;

            InitiateMultipartUploadRequest initiateRequest = CreateInitiateMutlipartUploadRequest(existingBucketName, "");
            InitiateMultipartUploadResponse initResponse = await s3Client.InitiateMultipartUploadAsync(initiateRequest);

            return upload;
        }

        public async Task UploadFileAsync(string existingBucketName, string keyName, string filePath, CancellationToken token)
        {
            bool hasException = false;

            // List to store upload part responses.
            List<UploadPartResponse> uploadResponses = new List<UploadPartResponse>();

            // 1. Initialize.
            InitiateMultipartUploadRequest initiateRequest = CreateInitiateMutlipartUploadRequest(existingBucketName, keyName);
            InitiateMultipartUploadResponse initResponse = await s3Client.InitiateMultipartUploadAsync(initiateRequest, token);

            // 2. Upload Parts.
            long contentLength = new FileInfo(filePath).Length;
            long partSize = 1048576; // 5 MB
            int coreCount = Environment.ProcessorCount;
            var parallelOptions = new ParallelOptions();
            //parallelOptions.MaxDegreeOfParallelism = 1;
            parallelOptions.CancellationToken = token;

            try
            {
                long filePosition = 0;
                var uploadPartTasks = new List<Task<UploadPartResponse>>();

                Console.WriteLine("Creating upload part tasks.");

                for (int i = 1; filePosition < contentLength; i++)
                {
                    var uploadPartTask =
                        BeginPartUploadAsync(existingBucketName, keyName, initResponse.UploadId, i, partSize, filePosition, filePath, token);

                    uploadPartTasks.Add(uploadPartTask);

                    filePosition += partSize;
                }

                Console.WriteLine("Finished creating upload part tasks.");

                Parallel.ForEach(uploadPartTasks, parallelOptions, async item =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            var uploadPartResponse = await item;

                            Console.WriteLine("Finished " + uploadPartResponse.PartNumber);

                            uploadResponses.Add(uploadPartResponse);
                        }
                    });

                //Parallel.For(1, coreCount, parallelOptions, async i =>
                //    {
                //        var relativeCounter = 0;

                //        while (filePosition < contentLength)
                //        {
                //            Console.WriteLine("Started uploading part " + (relativeCounter + i));

                //            var uploadPartResponse =
                //                await BeginUploadPartTask(existingBucketName, keyName, initResponse.UploadId, relativeCounter + i, partSize, filePosition, filePath, token);

                //            Console.WriteLine("Finished uploading part " + (relativeCounter + i));

                //            uploadResponses.Add(uploadPartResponse);

                //            filePosition += partSize;
                //            relativeCounter += 3;
                //        }
                //    });

                //for (int i = 1; filePosition < contentLength; i++)
                //{
                //    // Create request to upload a part.
                //    var uploadPartRequest = CreateUploadPartRequest(existingBucketName, keyName, initResponse.UploadId, i, partSize, filePosition, filePath);

                //    // Upload part and add response to our list.
                //    uploadPartRequest.StreamTransferProgress +=
                //        new EventHandler<StreamTransferProgressArgs>(UploadPartProgressEventCallback);

                //    var uploadPartResponse = await s3Client.UploadPartAsync(uploadPartRequest, token);
                //    uploadResponses.Add(uploadPartResponse);

                //    filePosition += partSize;
                //}

                // Step 3: complete.
                CompleteMultipartUploadRequest completeRequest = CreateCompleteMultipartUploadRequest(existingBucketName, keyName, initResponse.UploadId);

                CompleteMultipartUploadResponse completeUploadResponse = await s3Client.CompleteMultipartUploadAsync(completeRequest, token);

            }
            catch(TaskCanceledException e)
            {
                hasException = true;
                Console.WriteLine("Task cancelled");
            }
            catch (Exception exception)
            {
                hasException = true;
                //throw exception; // caller should handle and abort multipart upload

                Console.WriteLine("Exception occurred: {0}", exception.Message);
                AbortMultipartUploadRequest abortMPURequest = new AbortMultipartUploadRequest
                {
                    BucketName = existingBucketName,
                    Key = keyName,
                    UploadId = initResponse.UploadId
                };
                s3Client.AbortMultipartUpload(abortMPURequest);
            }
        }

        /// <summary>
        /// Asynchronously list all multipart pending uploads.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <returns></returns>
        public async Task<List<MultipartUpload>> ListMultiPartUploadsAsync(string existingBucketName)
        {
            ListMultipartUploadsRequest request = new ListMultipartUploadsRequest
            {
                BucketName = existingBucketName
            };

            ListMultipartUploadsResponse response = await s3Client.ListMultipartUploadsAsync(request);

            List<MultipartUpload> pendingUploads = response.MultipartUploads;

            return pendingUploads;
        }

        /// <summary>
        /// Asynchronously abort a multipart Upload operation.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="keyName"></param>
        /// <param name="uploadID"></param>
        /// <param name="cts"></param>
        /// <returns></returns>
        public async Task AbortMultiPartUploadAsync(string existingBucketName, string keyName, string uploadID, CancellationToken cts)
        {
            AbortMultipartUploadRequest request = new AbortMultipartUploadRequest()
            {
                BucketName = existingBucketName,
                Key = keyName,
                UploadId = uploadID
            };

            await s3Client.AbortMultipartUploadAsync(request, cts);
        }

        public void TrackUploadProgress()
        {
            UploadPartRequest uploadRequest = new UploadPartRequest
            {
                
            };

            uploadRequest.StreamTransferProgress += 
                new EventHandler<StreamTransferProgressArgs>(UploadPartProgressEventCallback);
        }

        public static void UploadPartProgressEventCallback(object sender, StreamTransferProgressArgs e)
        {
            // Process event. 
            Console.WriteLine("{0}/{1}", e.TransferredBytes, e.TotalBytes);
        }
    }
}
