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
        protected static readonly long PART_SIZE = 10 * 1024 * 1024;
        protected static readonly int PARALLEL_NUM = Environment.ProcessorCount - 1;

        public IAmazonS3 s3Client;

        private static AmazonS3Config _s3Config;
        private static string _accessKeyID;
        private static string _secretAccessKey;

        public Dictionary<int, long> MultipartUploadProgress = new Dictionary<int, long>();
        public long SingleUploadProgress = 0;

        public bool IsUploading = false;

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

        public void Setup(string accessKeyID, string secretAccessKey, string sessionToken)
        {
            try
            {
                // set the standard us region endpoint.
                _s3Config = new AmazonS3Config();
                _s3Config.RegionEndpoint = Amazon.RegionEndpoint.USEast1;
                _s3Config.ProgressUpdateInterval = 5 * 100 * 1024; // fire progress update event every 500 KB.

                s3Client = new AmazonS3Client(accessKeyID, secretAccessKey, sessionToken, _s3Config);
            }
            catch (Exception e) { throw e; }
        }


        #region examples
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
        #endregion






        /// <summary>
        /// Send a InitiateMultipartUploadRequest request and return the response.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="keyName"></param>
        /// <returns></returns>
        public async Task<InitiateMultipartUploadResponse> InitiateMultipartUpload(string existingBucketName, string keyName, CancellationToken token)
        {
            try
            {
                InitiateMultipartUploadRequest initiateRequest = new InitiateMultipartUploadRequest
                {
                    BucketName = existingBucketName,
                    Key = keyName
                };
                
                InitiateMultipartUploadResponse initResponse = await s3Client.InitiateMultipartUploadAsync(initiateRequest, token).ConfigureAwait(false);
                return initResponse;
            }
            catch (Exception e) { throw e; }
        }

        /// <summary>
        /// Send a ListPartsRequest request and return the response.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="keyName"></param>
        /// <param name="uploadID"></param>
        /// <param name="token"></param>
        /// <returns>Task<ListPartsResponse></returns>
        public async Task<ListPartsResponse> ListPartsAsync(string existingBucketName, string keyName, string uploadID, CancellationToken token)
        {
            try
            {
                ListPartsRequest request = new ListPartsRequest();
                request.BucketName = existingBucketName;
                request.Key = keyName;
                request.UploadId = uploadID;

                ListPartsResponse response = await this.s3Client.ListPartsAsync(request, token).ConfigureAwait(false);

                return response;
            }
            catch (Exception e) 
            { 
                throw e; 
            }
        }

        /// <summary>
        /// Send a CompleteMultipartUploadRequest request and return the response.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="keyName"></param>
        /// <param name="uploadID"></param>
        /// <returns>Task<CompleteMultipartUploadRequest></returns>
        public async Task<CompleteMultipartUploadRequest> CompleteMultipartUpload(string existingBucketName, string keyName, string uploadId, 
            CancellationToken token)
        {
            try
            {
                CompleteMultipartUploadRequest completeRequest = new CompleteMultipartUploadRequest
                {
                    BucketName = existingBucketName,
                    Key = keyName,
                    UploadId = uploadId,
                };

                // in any case request list parts to send etags for each part.
                var partsResponse = await this.ListPartsAsync(existingBucketName, keyName, uploadId, token).ConfigureAwait(false);
                List<PartETag> eTags = new List<PartETag>();

                foreach(var part in partsResponse.Parts)
                {
                    eTags.Add(new PartETag(part.PartNumber, part.ETag));
                }

                completeRequest.AddPartETags(eTags);

                CompleteMultipartUploadResponse completeResponse = await s3Client.CompleteMultipartUploadAsync(completeRequest, token).ConfigureAwait(false);

                return completeRequest;
            }
            catch (Exception e) { throw e; }
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

        /// <summary>
        /// Send an UploadPartRequest request and return its response. This call uploads part data.
        /// </summary>
        /// <param name="uploadPartRequest"></param>
        /// <param name="token"></param>
        /// <returns>Task<UploadPartResponse></returns>
        public async Task<UploadPartResponse> UploadPartAsync(UploadPartRequest uploadPartRequest, Dictionary<int, long> partsProgress, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();

                // Subscribe to progress event.
                //uploadPartRequest.StreamTransferProgress +=
                //    new EventHandler<StreamTransferProgressArgs>(UploadPartProgressEventCallback);

                uploadPartRequest.StreamTransferProgress += (sender, eventArgs) =>
                    {
                        partsProgress[uploadPartRequest.PartNumber] = eventArgs.TransferredBytes;
                        Console.WriteLine("Part " + uploadPartRequest.PartNumber + " transferred bytes: " + partsProgress[uploadPartRequest.PartNumber]);
                    };

                // Upload part and return response.
                var uploadPartResponse = await s3Client.UploadPartAsync(uploadPartRequest, token).ConfigureAwait(false);

                Console.WriteLine("Finished part " + uploadPartRequest.PartNumber);

                return uploadPartResponse;
            }
            catch (Exception e) { throw e; }
        }

        /// <summary>
        /// Single file upload to S3 bucket. Used only for file size less than 5 MB.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="info"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<bool> UploadSingleFileAsync(string existingBucketName, ArchiveFileInfo info, CancellationToken token)
        {
            try
            {
                this.IsUploading = true;

                token.ThrowIfCancellationRequested();

                this.SingleUploadProgress = 0;

                var putRequest = new PutObjectRequest()
                {
                    BucketName = existingBucketName,
                    Key = info.KeyName,
                    FilePath = info.FilePath
                };

                putRequest.StreamTransferProgress += (sender, eventArgs) =>
                {
                    this.SingleUploadProgress = eventArgs.TransferredBytes;
                    Console.WriteLine("Single Upload Progress: " + this.SingleUploadProgress);
                };

                var putResponse = await this.s3Client.PutObjectAsync(putRequest, token).ConfigureAwait(false);

                return putResponse.HttpStatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (Exception e) { throw e; }
            finally
            {
                this.IsUploading = false;
            }
        }

        public Queue<UploadPartRequest> PreparePartRequests(bool isNewFileUpload, string existingBucketName, ArchiveFileInfo fileInfo, 
            List<PartDetail> uploadedParts, Dictionary<int, long> partsProgress, CancellationToken token)
        {
            long filePosition = 0;
            long partSize = PART_SIZE; // 5 MB
            Queue<UploadPartRequest> partRequests = new Queue<UploadPartRequest>();

            if (fileInfo.Size < partSize)
                partSize = fileInfo.Size;

            for (int i = 1; filePosition < fileInfo.Size; i++)
            {
                var uploadedPart = uploadedParts.Where(x => x.PartNumber == i).FirstOrDefault();
                if (uploadedPart != null) // uploadedParts.Select(x => x.PartNumber).Contains(i))
                {
                    //partsProgress.Add(i, uploadedPart.Size); // for each already uploaded part, add total part size as transferred bytes.
                    filePosition += uploadedPart.Size;
                    continue;
                }

                // for each NOT uploaded part, add a pair in the progress dictionary with value = 0
                partsProgress.Add(i, 0);

                bool isLastPart = false;

                // check remaining size and if it's smalled than partSize
                // then set partSize = remainingSize and this as the last part in its request.
                long remainingSize = fileInfo.Size - filePosition;
                if (remainingSize <= partSize)
                {
                    isLastPart = true;
                    partSize = remainingSize;
                }

                // Create request to upload a part.
                var uploadPartRequest =
                    CreateUploadPartRequest(existingBucketName, fileInfo.KeyName, fileInfo.UploadId, i, partSize, filePosition, fileInfo.FilePath);

                // Now check if this is the last part and mark the request.
                if (isLastPart)
                    uploadPartRequest.IsLastPart = true;

                partRequests.Enqueue(uploadPartRequest);

                // increment position by partSize
                filePosition += partSize;
            }

            return partRequests;
        }

        public async Task<bool> UploadFileAsync(bool isNewFileUpload, string existingBucketName, ArchiveFileInfo fileInfo, CancellationToken token)
        {
            bool hasException = false;
            this.IsUploading = true;

            MultipartUploadProgress.Clear();

            ListPartsResponse partsResponse = new ListPartsResponse();

            var uploadPartTasks = new List<Task<UploadPartResponse>>();
            Queue<UploadPartRequest> partRequests = new Queue<UploadPartRequest>();
            List<Task<UploadPartResponse>> runningTasks = new List<Task<UploadPartResponse>>();

            try
            {
                token.ThrowIfCancellationRequested();

                // if this isn't a new file upload (resuming a past upload)
                // then we have to get the uploaded parts so the upload continues
                // where it's stopped.
                if (!isNewFileUpload)
                    partsResponse = await this.ListPartsAsync(existingBucketName, fileInfo.KeyName, fileInfo.UploadId, token).ConfigureAwait(false);

                token.ThrowIfCancellationRequested();

                Console.WriteLine("Creating upload part tasks.");

                // create all part requests.
                partRequests = this.PreparePartRequests(isNewFileUpload, existingBucketName, fileInfo, partsResponse.Parts, MultipartUploadProgress, token);

                Console.WriteLine("Finished creating upload part tasks.");

                token.ThrowIfCancellationRequested();

                // initialize first tasks to run.
                while (runningTasks.Count < PARALLEL_NUM && partRequests.Count > 0)
                {
                    var uploadTask = this.UploadPartAsync(partRequests.Dequeue(), MultipartUploadProgress, token);
                    runningTasks.Add(uploadTask);
                    Console.WriteLine("New task.");
                }

                while (runningTasks.Count > 0)
                {
                    token.ThrowIfCancellationRequested();

                    var finishedTask = await Task<UploadPartResponse>.WhenAny(runningTasks).ConfigureAwait(false);

                    runningTasks.Remove(finishedTask);
                    finishedTask.Dispose();

                    if (partRequests.Count > 0)
                    {
                        token.ThrowIfCancellationRequested();

                        var uploadTask = this.UploadPartAsync(partRequests.Dequeue(), MultipartUploadProgress, token);
                        runningTasks.Add(uploadTask);
                        Console.WriteLine("New task.");
                    }
                }

                // if all goes well, return true
                return true;
            }
            catch (Exception ex)
            {
                partRequests.Clear();
                runningTasks.Clear();

                throw ex;
            }
            finally
            {
                this.IsUploading = false;
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
            
            try
            {
                ListMultipartUploadsResponse response = await s3Client.ListMultipartUploadsAsync(request).ConfigureAwait(false);

                List<MultipartUpload> pendingUploads = response.MultipartUploads;

                return pendingUploads;
            }
            catch (Exception e) { throw e; }
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

            try
            {
                await s3Client.AbortMultipartUploadAsync(request, cts).ConfigureAwait(false);
            }
            catch(AggregateException ex)
            {
                throw ex;
            }
        }
    }
}
