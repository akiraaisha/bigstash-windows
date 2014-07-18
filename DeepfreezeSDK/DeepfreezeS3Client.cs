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
using log4net;

namespace DeepfreezeSDK
{
    [Export(typeof(IDeepfreezeS3Client))]
    public class DeepfreezeS3Client : IDeepfreezeS3Client
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(DeepfreezeS3Client));

        protected static readonly long PART_SIZE = 10 * 1024 * 1024;
        protected static readonly int PARALLEL_NUM = Environment.ProcessorCount;

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

        /// <summary>
        /// Send a InitiateMultipartUploadRequest request and return the response.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="keyName"></param>
        /// <returns></returns>
        public async Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(string existingBucketName, string keyName, CancellationToken token)
        {
            _log.Info("Called InitiateMultipartUploadAsync with parameter keyName = \"" + keyName + "\".");

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
            catch (Exception e)
            {
                string logMessage = "InitiateMultipartUploadAsync with parameter keyName = \"" + keyName + "\" threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".";

                if (e is AmazonS3Exception)
                {
                    logMessage += " ErrorType = " + ((AmazonS3Exception)e).ErrorType + ", ErrorCode = " + ((AmazonS3Exception)e).ErrorCode + ".";
                }

                _log.Error(logMessage);

                throw e;
            }
        }

        /// <summary>
        /// Send a ListPartsRequest request and return the response.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="keyName"></param>
        /// <param name="uploadID"></param>
        /// <param name="token"></param>
        /// <returns>Task<ListPartsResponse></returns>
        public async Task<ListPartsResponse> ListPartsAsync(string existingBucketName, string keyName, string uploadId, CancellationToken token)
        {
            _log.Info("Called ListPartsAsync with parameters keyName = \"" + keyName + "\" and uploadID = \"" + uploadId + "\".");

            try
            {
                ListPartsRequest request = new ListPartsRequest();
                request.BucketName = existingBucketName;
                request.Key = keyName;
                request.UploadId = uploadId;

                ListPartsResponse response = await this.s3Client.ListPartsAsync(request, token).ConfigureAwait(false);

                return response;
            }
            catch (Exception e) 
            {
                if (!(e is TaskCanceledException || e is OperationCanceledException))
                {
                    string logMessage = "ListPartsAsync with parameters keyName = \"" + keyName + "\" and uploadID = \"" + uploadId +
                        "\" threw " + e.GetType().Name + " with message \"" + e.Message + "\".";

                    if (e is AmazonS3Exception)
                    {
                        logMessage += " ErrorType = " + ((AmazonS3Exception)e).ErrorType + ", ErrorCode = " + ((AmazonS3Exception)e).ErrorCode + ".";
                    }

                    _log.Error(logMessage);
                }

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
        public async Task<CompleteMultipartUploadRequest> CompleteMultipartUploadAsync(string existingBucketName, string keyName, string uploadId, 
            CancellationToken token)
        {
            _log.Info("Called CompleteMultipartUploadAsync with parameters keyName = \"" + keyName + "\" and uploadID = \"" + uploadId + "\".");

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
            catch (Exception e) 
            {
                if (!(e is TaskCanceledException || e is OperationCanceledException))
                {
                    string logMessage = "CompleteMultipartUploadAsync with parameters keyName = \"" + keyName + "\" and uploadID = \"" + uploadId +
                        "\" threw " + e.GetType().Name + " with message \"" + e.Message + "\".";

                    if (e is AmazonS3Exception)
                    {
                        logMessage += " ErrorType = " + ((AmazonS3Exception)e).ErrorType + ", ErrorCode = " + ((AmazonS3Exception)e).ErrorCode + ".";
                    }

                    _log.Error(logMessage);
                }

                throw e;
            }
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
            _log.Info("Called UploadPartAsync with UploadPartRequest properties: KeyName = \"" + uploadPartRequest.Key + 
                "\", PartNumber = " + uploadPartRequest.PartNumber + ", PartSize = " + uploadPartRequest.PartSize +
                ", UploadId = \"" + uploadPartRequest.UploadId + ", FilePath = \"" + uploadPartRequest.FilePath + "\".");

            try
            {
                token.ThrowIfCancellationRequested();

                uploadPartRequest.StreamTransferProgress += (sender, eventArgs) =>
                    {
                        partsProgress[uploadPartRequest.PartNumber] = eventArgs.TransferredBytes;
                        Console.WriteLine("Part " + uploadPartRequest.PartNumber + " transferred bytes: " + partsProgress[uploadPartRequest.PartNumber]);
                    };

                // Upload part and return response.
                var uploadPartResponse = await s3Client.UploadPartAsync(uploadPartRequest, token).ConfigureAwait(false);

                _log.Info("Finished UploadPartAsync with UploadPartRequest properties: KeyName = \"" + uploadPartRequest.Key +
                "\", PartNumber = " + uploadPartRequest.PartNumber + ", PartSize = " + uploadPartRequest.PartSize +
                ", UploadId = \"" + uploadPartRequest.UploadId + ", FilePath = \"" + uploadPartRequest.FilePath + "\".");

                return uploadPartResponse;
            }
            catch (Exception e)
            {
                if (!(e is TaskCanceledException || e is OperationCanceledException))
                {
                    var logMessage = "UploadPartAsync with UploadPartRequest properties: KeyName = \"" + uploadPartRequest.Key +
                        "\", PartNumber = " + uploadPartRequest.PartNumber + ", PartSize = " + uploadPartRequest.PartSize +
                        ", UploadId = \"" + uploadPartRequest.UploadId + ", FilePath = \"" + uploadPartRequest.FilePath +
                        "\" threw " + e.GetType().Name + " with message \"" + e.Message + "\".";

                    if (e is AmazonS3Exception)
                    {
                        logMessage += " ErrorType = " + ((AmazonS3Exception)e).ErrorType + ", ErrorCode = " + ((AmazonS3Exception)e).ErrorCode + ".";
                    }

                    _log.Error(logMessage);
                }

                throw e;
            }
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
            _log.Info("Called UploadSingleFileAsync with ArchiveFileInfo properties: KeyName = \"" + info.KeyName +
                "\", FilePath = \"" + info.FilePath + "\".");

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
            catch (Exception e) 
            {
                if (!(e is TaskCanceledException || e is OperationCanceledException))
                {
                    var logMessage = "UploadSingleFileAsync with ArchiveFileInfo properties: KeyName = \"" + info.KeyName +
                        "\", FilePath = \"" + info.FilePath + "\" threw " + e.GetType().Name + " with message \"" + e.Message + "\".";

                    if (e is AmazonS3Exception)
                    {
                        logMessage += " ErrorType = " + ((AmazonS3Exception)e).ErrorType + ", ErrorCode = " + ((AmazonS3Exception)e).ErrorCode + ".";
                    }

                    _log.Error(logMessage);
                }

                throw e;
            }
            finally
            {
                this.IsUploading = false;
            }
        }

        /// <summary>
        /// Create UploadPartRequest objects for a multipart upload.
        /// </summary>
        /// <param name="isNewFileUpload"></param>
        /// <param name="existingBucketName"></param>
        /// <param name="fileInfo"></param>
        /// <param name="uploadedParts"></param>
        /// <param name="partsProgress"></param>
        /// <param name="token"></param>
        /// <returns></returns>
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

        public async Task<bool> UploadMultipartFileAsync(bool isNewFileUpload, string existingBucketName, ArchiveFileInfo fileInfo,
            CancellationTokenSource cts, CancellationToken token)
        {
            _log.Info("Called UploadMultipartFileAsync with ArchiveFileInfo properties: KeyName = \"" + fileInfo.KeyName +
                "\", FilePath = \"" + fileInfo.FilePath + "\".");

            bool hasException = false;
            this.IsUploading = true;

            this.MultipartUploadProgress.Clear();

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

                // create all part requests.
                partRequests = this.PreparePartRequests(isNewFileUpload, existingBucketName, fileInfo, partsResponse.Parts, this.MultipartUploadProgress, token);

                token.ThrowIfCancellationRequested();

                // initialize first tasks to run.
                while (runningTasks.Count < PARALLEL_NUM && partRequests.Count > 0)
                {
                    var uploadTask = this.UploadPartAsync(partRequests.Dequeue(), this.MultipartUploadProgress, token);
                    runningTasks.Add(uploadTask);
                }

                while (runningTasks.Count > 0)
                {
                    token.ThrowIfCancellationRequested();

                    var finishedTask = await Task<UploadPartResponse>.WhenAny(runningTasks).ConfigureAwait(false);

                    runningTasks.Remove(finishedTask);

                    if (finishedTask.Status == TaskStatus.Faulted)
                        throw finishedTask.Exception;

                    if (partRequests.Count > 0)
                    {
                        token.ThrowIfCancellationRequested();

                        var uploadTask = this.UploadPartAsync(partRequests.Dequeue(), this.MultipartUploadProgress, token);
                        runningTasks.Add(uploadTask);
                    }
                }

                token.ThrowIfCancellationRequested();

                // if all goes well, return true
                return true;
            }
            catch (Exception ex)
            {
                hasException = true;

                partRequests.Clear();
                runningTasks.Clear();

                throw ex;
            }
            finally
            {
                if (hasException && cts != null && !cts.IsCancellationRequested)
                    cts.Cancel();

                this.IsUploading = false;
            }
        }

        /// <summary>
        /// Asynchronously abort a multipart Upload operation.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="keyName"></param>
        /// <param name="uploadID"></param>
        /// <param name="cts"></param>
        /// <returns></returns>
        public async Task AbortMultiPartUploadAsync(string existingBucketName, string keyName, string uploadId, CancellationToken cts)
        {
            _log.Info("Called AbortMultiPartUploadAsync with parameters keyName = \"" + keyName +
                "\", UploadId = \"" + uploadId + "\".");

            AbortMultipartUploadRequest request = new AbortMultipartUploadRequest()
            {
                BucketName = existingBucketName,
                Key = keyName,
                UploadId = uploadId
            };

            try
            {
                await s3Client.AbortMultipartUploadAsync(request, cts).ConfigureAwait(false);
            }
            catch(Exception e)
            {
                if (!(e is TaskCanceledException || e is OperationCanceledException))
                {
                    string logMessage = "AbortMultiPartUploadAsync with parameters keyName = \"" + keyName +
                        "\", UploadId = \"" + uploadId + "\" threw " + e.GetType().Name + " with message \"" + e.Message + "\".";

                    if (e is AmazonS3Exception)
                    {
                        logMessage += " ErrorType = " + ((AmazonS3Exception)e).ErrorType + ", ErrorCode = " + ((AmazonS3Exception)e).ErrorCode + ".";
                    }

                    _log.Error(logMessage);
                }

                throw e;
            }
        }
    }
}
