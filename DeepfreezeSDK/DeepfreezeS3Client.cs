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

        protected static readonly long PART_SIZE = 5 * 1024 * 1024;
        protected static readonly int MAX_PARALLEL_ALLOWED = Environment.ProcessorCount - 1;

        private static AmazonS3Config _s3Config;

        public IAmazonS3 s3Client;

        public bool IsUploading = false;

        public DeepfreezeS3Client()
        { }

        /// <summary>
        /// Setup the S3 Client with the user credentials and the correct S3 Configuation.
        /// </summary>
        /// <param name="accessKeyID"></param>
        /// <param name="secretAccessKey"></param>
        //public void Setup(string accessKeyID, string secretAccessKey)
        //{
        //    _s3Config = new AmazonS3Config() { RegionEndpoint = Amazon.RegionEndpoint.EUWest1 };
        //    s3Client = AWSClientFactory.CreateAmazonS3Client(accessKeyID, secretAccessKey, _s3Config);
        //    _accessKeyID = accessKeyID;
        //    _secretAccessKey = secretAccessKey;
        //}

        public void Setup(string accessKeyID, string secretAccessKey, string sessionToken)
        {
            try
            {
                // set the standard us region endpoint.
                _s3Config = new AmazonS3Config();
                _s3Config.RegionEndpoint = Amazon.RegionEndpoint.USWest2;
                _s3Config.ProgressUpdateInterval = 2 * 100 * 1024; // fire progress update event every 500 KB.
                s3Client = new AmazonS3Client(accessKeyID, secretAccessKey, sessionToken, _s3Config);
            }
            catch (Exception) { throw; }
        }

        /// <summary>
        /// Send a InitiateMultipartUploadRequest request and return the response.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="keyName"></param>
        /// <returns></returns>
        public async Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(string existingBucketName, string keyName, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            _log.Debug("Called InitiateMultipartUploadAsync with parameter keyName = \"" + keyName + "\".");

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
                string messagePart = " with parameter keyName = \"" + keyName + "\"";
                this.LogAmazonException(messagePart, e);

                throw;
            }
        }

        /// <summary>
        /// Send a ListPartsRequest request and return a list of all uploaded parts.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="keyName"></param>
        /// <param name="uploadID"></param>
        /// <param name="token"></param>
        /// <returns>Task<List<PartDetail>></returns>
        public async Task<List<PartDetail>> ListPartsAsync(string existingBucketName, string keyName, string uploadId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            _log.Debug("Called ListPartsAsync with parameters keyName = \"" + keyName + "\" and uploadID = \"" + uploadId + "\".");

            List<PartDetail> parts = new List<PartDetail>();

            try
            {
                ListPartsRequest request = new ListPartsRequest();
                request.BucketName = existingBucketName;
                request.Key = keyName;
                request.UploadId = uploadId;

                ListPartsResponse response = await this.s3Client.ListPartsAsync(request, token).ConfigureAwait(false);
                parts.AddRange(response.Parts);

                while(response.IsTruncated)
                {
                    token.ThrowIfCancellationRequested();

                    request.PartNumberMarker = response.NextPartNumberMarker.ToString();
                    response = await this.s3Client.ListPartsAsync(request, token).ConfigureAwait(false);
                    parts.AddRange(response.Parts);
                }

                return parts;
            }
            catch (Exception e) 
            {
                if (!(e is TaskCanceledException || e is OperationCanceledException))
                {
                    string messagePart = " with parameters keyName = \"" + keyName + "\" and uploadID = \"" + uploadId + "\"";

                    this.LogAmazonException(messagePart, e);
                }

                throw;
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
            token.ThrowIfCancellationRequested();

            _log.Debug("Called CompleteMultipartUploadAsync with parameters keyName = \"" + keyName + "\" and uploadID = \"" + uploadId + "\".");

            try
            {
                CompleteMultipartUploadRequest completeRequest = new CompleteMultipartUploadRequest
                {
                    BucketName = existingBucketName,
                    Key = keyName,
                    UploadId = uploadId,
                };

                // in any case request list parts to send etags for each part.
                var uploadedParts = await this.ListPartsAsync(existingBucketName, keyName, uploadId, token).ConfigureAwait(false);
                List<PartETag> eTags = new List<PartETag>();

                foreach (var part in uploadedParts)
                {
                    eTags.Add(new PartETag(part.PartNumber, part.ETag));
                }

                completeRequest.AddPartETags(eTags);

                token.ThrowIfCancellationRequested();

                CompleteMultipartUploadResponse completeResponse = await s3Client.CompleteMultipartUploadAsync(completeRequest, token).ConfigureAwait(false);

                return completeRequest;
            }
            catch (Exception e) 
            {
                if (!(e is TaskCanceledException || e is OperationCanceledException))
                {
                    string messagePart = " with parameters keyName = \"" + keyName + "\" and uploadID = \"" + uploadId + "\"";

                    this.LogAmazonException(messagePart, e);
                }

                throw;
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
        public async Task<UploadPartResponse> UploadPartAsync(UploadPartRequest uploadPartRequest, Dictionary<int, long> multipartUploadProgress, CancellationToken token, IProgress<Tuple<string,long>> progress = null)
        {
            token.ThrowIfCancellationRequested();

            _log.Debug("Called UploadPartAsync with UploadPartRequest properties: KeyName = \"" + uploadPartRequest.Key + 
                "\", PartNumber = " + uploadPartRequest.PartNumber + ", PartSize = " + uploadPartRequest.PartSize +
                ", UploadId = \"" + uploadPartRequest.UploadId + ", FilePath = \"" + uploadPartRequest.FilePath + "\".");

            int retries = 2;

            uploadPartRequest.StreamTransferProgress += (sender, eventArgs) =>
            {
                if (progress != null)
                {
                    multipartUploadProgress[uploadPartRequest.PartNumber] = eventArgs.TransferredBytes;
                    var multiPartProgress = multipartUploadProgress.Sum(x => x.Value);
                    progress.Report(Tuple.Create(uploadPartRequest.Key, multiPartProgress));
                }

                Console.WriteLine(uploadPartRequest.Key + " - part " + uploadPartRequest.PartNumber + ": " + eventArgs.TransferredBytes + " / " + eventArgs.TotalBytes + " bytes.");
            };

            while (true)
            {
                try
                {
                    token.ThrowIfCancellationRequested();

                    // Upload part and return response.
                    var uploadPartResponse = await s3Client.UploadPartAsync(uploadPartRequest, token).ConfigureAwait(false);

                    _log.Debug("Successfully uploaded KeyName = \"" + uploadPartRequest.Key +
                                "\", PartNumber = " + uploadPartRequest.PartNumber + "\".");

                    return uploadPartResponse;
                }
                catch (Exception e)
                {
                    if (!(e is TaskCanceledException || e is OperationCanceledException))
                    {
                        var messagePart = " with UploadPartRequest properties: KeyName = \"" + uploadPartRequest.Key +
                            "\", PartNumber = " + uploadPartRequest.PartNumber + ", PartSize = " + uploadPartRequest.PartSize +
                            ", UploadId = \"" + uploadPartRequest.UploadId + ", FilePath = \"" + uploadPartRequest.FilePath + "\"";

                        this.LogAmazonException(messagePart, e);

                        // if the exception is AmazonS3Exception or AmazonServiceException with error type Sender,
                        // which means that the client is responsible for the error,
                        // then throw do not retry.
                        if (e is AmazonS3Exception)
                        {
                            var ae = e as AmazonS3Exception;

                            if (ae.ErrorType == ErrorType.Sender)
                                throw;
                        }
                        if (e is AmazonServiceException)
                        {
                            var ae = e as AmazonServiceException;

                            if (ae.ErrorType == ErrorType.Sender)
                                throw;
                        }

                        if (--retries == 0)
                            throw;
                    }
                    // if the action is paused, throw an exception. The loop is broken.
                    else
                        throw;
                }
            }
        }

        /// <summary>
        /// Single file upload to S3 bucket. Used only for file size less than 5 MB.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="info"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<bool> UploadSingleFileAsync(string existingBucketName, string keyName, string path, CancellationToken token = default(CancellationToken), IProgress<Tuple<string, long>> progress = null)
        {
            token.ThrowIfCancellationRequested();

            _log.Debug("Called UploadSingleFileAsync with ArchiveFileInfo properties: KeyName = \"" + keyName +
                "\", FilePath = \"" + path + "\".");

            var putRequest = new PutObjectRequest()
            {
                BucketName = existingBucketName,
                Key = keyName,
                FilePath = path,
            };

            putRequest.StreamTransferProgress += (sender, eventArgs) =>
            {
                if (progress != null)
                {
                    progress.Report(Tuple.Create(keyName, eventArgs.TransferredBytes));
                }

                Console.WriteLine(keyName + ": " + eventArgs.TransferredBytes + " / " + eventArgs.TotalBytes + " bytes.");
            };

            int retries = 2;

            while (true)
            {
                this.IsUploading = true;

                try
                {
                    token.ThrowIfCancellationRequested();

                    var putResponse = await this.s3Client.PutObjectAsync(putRequest, token).ConfigureAwait(false);

                    _log.Debug("Successfully uploaded KeyName = \"" + keyName + "\".");

                    return putResponse.HttpStatusCode == System.Net.HttpStatusCode.OK;
                }
                catch (Exception e)
                {
                    if (!(e is TaskCanceledException || e is OperationCanceledException))
                    {
                        var messagePart = " with ArchiveFileInfo properties: KeyName = \"" + keyName +
                            "\", FilePath = \"" + path + "\"";

                        this.LogAmazonException(messagePart, e);

                        // if the exception is AmazonS3Exception or AmazonServiceException with error type Sender,
                        // which means that the client is responsible for the error,
                        // then throw do not retry.
                        if (e is AmazonS3Exception)
                        {
                            var ae = e as AmazonS3Exception;

                            if (ae.ErrorType == ErrorType.Sender)
                                throw;
                        }
                        if (e is AmazonServiceException)
                        {
                            var ae = e as AmazonServiceException;

                            if (ae.ErrorType == ErrorType.Sender)
                                throw;
                        }

                        if (--retries == 0)
                            throw;
                    }
                    // if the action is paused, throw an exception. The loop is broken.
                    else
                        throw;
                }
                finally
                {
                    this.IsUploading = false;
                }
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
            List<PartDetail> uploadedParts, Dictionary<int, long> partsProgress)
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
                    // for each already uploaded part, add total part size as transferred bytes.
                    partsProgress.Add(i, uploadedPart.Size); 
                    // move the file position
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
            CancellationTokenSource cts, CancellationToken token, IProgress<Tuple<string, long>> progress)
        {
            token.ThrowIfCancellationRequested();

            _log.Debug("Called UploadMultipartFileAsync with ArchiveFileInfo properties: KeyName = \"" + fileInfo.KeyName +
                "\", FilePath = \"" + fileInfo.FilePath + "\".");

            bool hasException = false;
            this.IsUploading = true;

            List<PartDetail> uploadedParts = new List<PartDetail>();
            Dictionary<int, long> multipartUploadProgress = new Dictionary<int, long>();

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
                    uploadedParts = await this.ListPartsAsync(existingBucketName, fileInfo.KeyName, fileInfo.UploadId, token).ConfigureAwait(false);

                token.ThrowIfCancellationRequested();

                // create all part requests.
                partRequests = this.PreparePartRequests(isNewFileUpload, existingBucketName, fileInfo, uploadedParts, multipartUploadProgress);

                token.ThrowIfCancellationRequested();

                var parallelLimit = (MAX_PARALLEL_ALLOWED > 1) ? MAX_PARALLEL_ALLOWED : 2;

                // initialize first tasks to run.
                while (runningTasks.Count < parallelLimit && partRequests.Count > 0)
                {
                    var uploadTask = this.UploadPartAsync(partRequests.Dequeue(), multipartUploadProgress, token, progress);
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

                        var uploadTask = this.UploadPartAsync(partRequests.Dequeue(), multipartUploadProgress, token, progress);
                        runningTasks.Add(uploadTask);
                    }

                    finishedTask = null;
                }

                token.ThrowIfCancellationRequested();

                // if all goes well, return true
                return true;
            }
            catch (Exception)
            {
                hasException = true;

                partRequests.Clear();
                runningTasks.Clear();
                multipartUploadProgress.Clear();
                uploadedParts.Clear();
                uploadPartTasks.Clear();

                throw;
            }
            finally
            {
                if (hasException && cts != null && !cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }

                uploadedParts.Clear();
                multipartUploadProgress.Clear();
                uploadPartTasks.Clear();
                partRequests.Clear();
                runningTasks.Clear();

                this.IsUploading = false;
            }
        }

        /// <summary>
        /// Asynchronously abort a multipart Upload operation.
        /// </summary>
        /// <param name="existingBucketName"></param>
        /// <param name="keyName"></param>
        /// <param name="uploadID"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task AbortMultiPartUploadAsync(string existingBucketName, string keyName, string uploadId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            _log.Debug("Called AbortMultiPartUploadAsync with parameters keyName = \"" + keyName +
                "\", UploadId = \"" + uploadId + "\".");

            AbortMultipartUploadRequest request = new AbortMultipartUploadRequest()
            {
                BucketName = existingBucketName,
                Key = keyName,
                UploadId = uploadId
            };

            try
            {
                await s3Client.AbortMultipartUploadAsync(request, token).ConfigureAwait(false);
            }
            catch(Exception e)
            {
                if (!(e is TaskCanceledException || e is OperationCanceledException))
                {
                    string messagePart = " with parameters keyName = \"" + keyName +
                        "\", UploadId = \"" + uploadId + "\"";

                    this.LogAmazonException(messagePart, e);
                }

                throw;
            }
        }

        #region private_methods

        /// <summary>
        /// Handle amazon exceptions occuring while trying requests to the AWS API.
        /// </summary>
        /// <param name="innerException"></param>
        /// <param name="memberName"></param>
        /// <returns></returns>
        private void LogAmazonException(string message, Exception ex,
                                      [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            StringBuilder errorMsg = new StringBuilder();
            errorMsg.Append(memberName);
            errorMsg.Append(message);
            errorMsg.AppendLine(" threw exception:");

            if (ex is AmazonS3Exception)
            {
                var ae = ex as AmazonS3Exception;

                errorMsg.Append("ErrorType = ");
                errorMsg.AppendLine(ae.ErrorType.ToString());
                errorMsg.Append("ErrorCode = ");
                errorMsg.Append(ae.ErrorCode);
            }

            _log.Error(errorMsg.ToString(), ex);
        }

        #endregion
    }
}
