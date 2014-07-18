using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Threading;
using System.IO;

using Caliburn.Micro;

using DeepfreezeSDK;
using DeepfreezeModel;

using Amazon.S3.Model;
using Amazon.Runtime;
using System.Windows.Threading;
using System.Net.Http;
using Amazon.S3;

namespace DeepfreezeApp
{
    [Export(typeof(IUploadViewModel))]
    public class UploadViewModel : Screen, IUploadViewModel, IHandleWithTask<IUploadActionMessage>
    {
        #region fields
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(UploadViewModel));

        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private bool _isBusy = false;
        private bool _isUploading = false;
        private bool _isRefreshing = false;
        private bool _currentUploadIsMultipart = false;
        private string _errorMessage;
        private long _progress = 0;
        private string _busyMessage;

        private Archive _archive;
        private Upload _upload;
        private LocalUpload _localUpload;
        private ArchiveFileInfo _currentFileInfo;

        private long _currentFileProgress = 0;
        private long _totalProgress = 0; // this is updated when a file completes upload and not while uploading.

        private DeepfreezeS3Client _s3Client = new DeepfreezeS3Client();
        private S3Info _s3Info = new S3Info();

        private CancellationTokenSource _cts;

        private Enumerations.Status _operationStatus;

        private readonly long MIN_FILE_SIZE_FOR_MULTI_PART_UPLOAD = 10 * 1024 * 1024;

        private DispatcherTimer _refreshProgressTimer = new DispatcherTimer();

        #endregion

        #region constructor
        [ImportingConstructor]
        public UploadViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;

            this._refreshProgressTimer.Tick += Tick;
            this._refreshProgressTimer.Interval = new TimeSpan(0, 0, 5);
        }

        #endregion

        #region properties

        public bool IsBusy
        {
            get { return this._isBusy; }
            set { this._isBusy = value; NotifyOfPropertyChange(() => this.IsBusy); }
        }

        public bool IsUploading
        {
            get { return this._isUploading; }
            set { this._isUploading = value; NotifyOfPropertyChange(() => this.IsUploading); }
        }

        public string ErrorMessage
        {
            get { return this._errorMessage; }
            set { this._errorMessage = value; NotifyOfPropertyChange(() => this.ErrorMessage); }
        }

        public string BusyMessage
        {
            get { return this._busyMessage; }
            set { this._busyMessage = value; NotifyOfPropertyChange(() => this.BusyMessage); }
        }

        public long Progress
        {
            get { return this._progress; }
            set 
            { 
                this._progress = value; 
                NotifyOfPropertyChange(() => this.Progress);
                NotifyOfPropertyChange(() => this.ProgressText);
            }
        }

        public string ProgressText
        {
            get 
            {
                if (this.Archive != null)
                {
                    var sb = new StringBuilder();
                    sb.Append("  ");
                    var percentage = ((double)this.Progress / this.Archive.Size) * 100;
                    sb.Append((int)percentage);
                    sb.Append(Properties.Resources.PercentageOfText);
                    sb.Append(LongToSizeString.ConvertToString(this.Archive.Size));

                    return sb.ToString();
                }
                else
                    return null;
            }
        }

        public Archive Archive
        {
            get { return this._archive; }
            set
            {
                if (value != null)
                {
                    this._archive = value;
                    NotifyOfPropertyChange(() => this.Archive);
                    //this._totalSizeString = LongToSizeString.ConvertToString(this._archive.Size);
                }
                else
                {

                }
            }
        }

        public Upload Upload
        {
            get { return this._upload; }
            set
            { 
                this._upload = value;
                NotifyOfPropertyChange(() => this.Upload);
            }
        }

        public LocalUpload LocalUpload
        {
            get { return this._localUpload; }
            set { this._localUpload = value; }
        }

        public ArchiveFileInfo CurrentFileInfo
        {
            get { return this._currentFileInfo; }
            set 
            {
                this._currentFileInfo = value;
                NotifyOfPropertyChange(() => this.CurrentFileInfo); 
            }
        }

        public Enumerations.Status OperationStatus
        {
            get { return this._operationStatus; }
            set 
            { 
                this._operationStatus = value;
                NotifyOfPropertyChange(() => this.OperationStatus); 
            }
        }

        #endregion

        #region action_methods

        /// <summary>
        /// Start the upload operation. This method is responsible for calling the respective
        /// DeepfreezeS3Client methods for initiating and starting Amazon S3 uploads. This operation
        /// is cancellable using a CancellationToken, which is provided in every upload method used
        /// by the DeepfreezeS3Client, either for single file upload or for multipart upload. Also, 
        /// this method is responsible for starting and stopping the refresh timer for progress updates,
        /// as well as for handling the completion part of the upload, including the completion of 
        /// multipart upload and the completion of the Deepfreeze Upload. While this is running it has 
        /// access in the local upload file, updating it any time a file is uploaded.
        /// </summary>
        /// <returns></returns>
        public async Task StartUpload()
        {
            bool hasException = false;

            this.IsBusy = false;
            this.IsUploading = true;
            this.OperationStatus = Enumerations.Status.Uploading;
            this.ErrorMessage = null;

            this._cts = new CancellationTokenSource();
            CancellationToken token = this._cts.Token;

            _log.Info("Starting (user clicked the Start button) archive upload with title \"" + this.Archive.Title + "\".");

            // skip files with IsUploaded = true entirely.
            var lstFilesToUpload = this.LocalUpload.ArchiveFilesInfo.Where(x => !x.IsUploaded).ToList();

            var skippedFilesNum = this.LocalUpload.ArchiveFilesInfo.Count - lstFilesToUpload.Count;
            if (skippedFilesNum > 0)
            {
                _log.Info("Archive upload with title \"" + this.Archive.Title + "\", skipping " + skippedFilesNum +
                    " files since they are already uploaded.");
            }

            try
            {
                foreach (var info in lstFilesToUpload)
                {
                    CurrentFileInfo = info;
                    this._currentFileProgress = 0;

                    _log.Info("Start uploading file: \"" + info.FileName + "\".");

                    // if UploadId is null then we mark this file info as a completely new S3 upload
                    // else it's an upload started in the past.
                    bool isNewFileUpload = (info.UploadId == null);

                    if (!isNewFileUpload && info.LastModified < new FileInfo(info.FilePath).LastWriteTimeUtc)
                    {
                        throw new Exception("The file " + info.FileName + " has changed since you selected it for archiving.\nCancel the upload and create a new archive.");
                    }

                    // Check if there is already an uploadID for the current file upload.
                    // If not initiate a new S3 upload and set the UploadId property.
                    if (isNewFileUpload && info.Size > MIN_FILE_SIZE_FOR_MULTI_PART_UPLOAD)
                    {
                        InitiateMultipartUploadResponse initResponse =
                            await this._s3Client.InitiateMultipartUploadAsync(this._s3Info.Bucket, info.KeyName, token).ConfigureAwait(false);

                        info.UploadId = initResponse.UploadId;

                        await this.SaveLocalUpload();
                    }

                    bool uploadFinished = false;
                    this._refreshProgressTimer.Start();

                    this._currentUploadIsMultipart = info.Size > MIN_FILE_SIZE_FOR_MULTI_PART_UPLOAD;

                    if (this._currentUploadIsMultipart)
                    {
                        // If this file info has an UploadId and IsUploaded = false, then proceed with uploading the file.
                        uploadFinished = await this._s3Client.UploadMultipartFileAsync(isNewFileUpload, this._s3Info.Bucket, info, this._cts, token).ConfigureAwait(false);

                        if (uploadFinished)
                        {
                            this._refreshProgressTimer.Stop();

                            // send a complete request to finish the s3 upload.
                            var completeResponse = await this._s3Client.CompleteMultipartUploadAsync(this._s3Info.Bucket, info.KeyName, info.UploadId, token)
                                .ConfigureAwait(false);

                            // set the UploadId to null since it's completed and no longer exists.
                            info.UploadId = null;
                        }
                    }
                    else
                    {
                        uploadFinished = await this._s3Client.UploadSingleFileAsync(this._s3Info.Bucket, info, token).ConfigureAwait(false);
                        this._refreshProgressTimer.Stop();
                    }

                    _log.Info("Finished uploading file: \"" + info.FileName + "\".");

                    info.IsUploaded = uploadFinished;
                    await this.SaveLocalUpload();

                    // So, at this point, a file finished uploading, so we check the total uploaded bytes.
                    await this.CalculateTotalUploadedSize().ConfigureAwait(false);
                    this.Progress = this._totalProgress;
                }

                // Since all files are uploaded send a patch to upload url with status uploaded to complete it.
                this.Upload = await this._deepfreezeClient.FinishUploadAsync(this.Upload).ConfigureAwait(false);

                _log.Info("Finished archive upload with title \"" + this.Archive.Title + "\".");

                // again make sure progress is reported correctly.
                await this.CalculateTotalUploadedSize().ConfigureAwait(false);
                this.Progress = this._totalProgress;

                this.OperationStatus = this.Upload.Status;
            }
            catch (Exception e)
            {
                this._refreshProgressTimer.Stop();
                this.OperationStatus = Enumerations.Status.Paused;
                hasException = true;

                if (!(e is TaskCanceledException || e is OperationCanceledException))
                {
                    this.ErrorMessage = e.Message;

                    if (e is AggregateException)
                    {
                        foreach(var inner in ((AggregateException)e).InnerExceptions)
                        {
                            this.ErrorMessage += "\n" + inner.Message;
                            if (inner.InnerException != null)
                                this.ErrorMessage += "\n" + inner.InnerException.Message;
                        }
                    }
                }
            }

            // if the user paused, we have to update the total progress, as some parts probably got aborted before finishing.
            if (hasException)
            {
                // check again for timer stop
                this._refreshProgressTimer.Stop();
                // in case of an exception other than operation cancelled (which is thrown by the user's pause action,
                // make sure to actually send a cancel to all remaining uploading part tasks to abort them.
                if (this._cts != null && !this._cts.IsCancellationRequested)
                    await this.PauseUpload(true);

                await this.CalculateTotalUploadedSize().ConfigureAwait(false);

                // finally make sure to save the local upload file to preserve the current status of the upload.
                await this.SaveLocalUpload();
            }

            IsUploading = false;
            IsBusy = false;
        }

        /// <summary>
        /// Pause the upload operation after the specified cancelAfter value (in ms).
        /// Essentially, this cancels the token provided earlier in any upload running task
        /// in the StartUpload method. Also, this method stops the refresh timer.
        /// </summary>
        /// <param name="cancelAfter"></param>
        /// <returns></returns>
        public async Task PauseUpload(bool isAutomatic) //wait 500 ms to be sure that the UI updates.
        {
            if (this.OperationStatus == Enumerations.Status.Uploading)
            {
                this.IsBusy = true;
                this.BusyMessage = "Pausing upload...";
                this.OperationStatus = Enumerations.Status.Paused;

                this._refreshProgressTimer.Stop();

                var originalAction = isAutomatic ? "(automatic pause)" : "(user clicked the Pause button)";
                _log.Info("Pausing " + originalAction + " archive upload with title \"" + this.Archive.Title + "\".");

                if (this._cts != null)
                    await Task.Run(() => 
                        {
                            if (this._cts != null)
                                this._cts.Cancel();
                        }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Delete the Upload. This pauses the upload operation (if it's running),
        /// aborts any Amazon S3 Multipart Uploads, deletes the upload resource and 
        /// deletes the local upload file from the user's disk. Finally it publishes a 
        /// RemoveUploadMessage for the UploadManagerViewModel to handle and remove this
        /// UploadViewModel from its Uploads list.
        /// </summary>
        /// <returns></returns>
        public async Task DeleteUpload()
        {
            try
            {
                _log.Info("Deleting (user clicked the Delete button) archive upload with title \"" + this.Archive.Title + "\".");

                this.IsBusy = true;
                this.BusyMessage = "Deleting upload...";

                await this.PauseUpload(true);

                if (this.Upload != null && 
                    this.Upload.Status != Enumerations.Status.Completed &&
                    this.Upload.Status != Enumerations.Status.Uploaded)
                {
                    var abortSuccess = await this.Abort().ConfigureAwait(false);

                    var deleteSuccess = await this._deepfreezeClient.DeleteUploadAsync(this.Upload).ConfigureAwait(false);
                }

                this.DeleteLocalUpload();

                var removeUploadMessage = IoC.Get<IRemoveUploadViewModelMessage>();
                removeUploadMessage.UploadVMToRemove = this;
                this._eventAggregator.PublishOnBackgroundThread(removeUploadMessage);
            }
            catch (Exception e)
            {
                this.ErrorMessage = e.Message;
            }
            finally
            { this.IsBusy = false; }
        }

        public void RemoveUpload()
        {
            try
            {
                _log.Info("Removing (user clicked the Remove button) completed archive upload with title \"" + this.Archive.Title + "\".");

                this.DeleteLocalUpload();

                var removeUploadMessage = IoC.Get<IRemoveUploadViewModelMessage>();
                removeUploadMessage.UploadVMToRemove = this;
                this._eventAggregator.PublishOnBackgroundThread(removeUploadMessage);
            }
            catch(Exception e)
            {
                this.ErrorMessage = e.Message;
            }
        }

        /// <summary>
        /// Refresh upload action. This essentialy calls asynchronously the 
        /// PrepareUploadAsync private method.
        /// </summary>
        /// <returns></returns>
        public async Task RefreshUpload()
        {
            this._isRefreshing = true;
            this.ErrorMessage = null;

            _log.Info("Refreshing upload (user clicked refresh button) in upload manager.");

            try
            {
                await this.PrepareUploadAsync().ConfigureAwait(false);
            }
            catch(Exception e) 
            {
                this.ErrorMessage = e.Message;
            }
            finally { this._isRefreshing = false; }
        }

        #endregion

        #region private_methods

        /// <summary>
        /// Create a new upload by sending a post to the upload url.
        /// This method assumes that the VM already has an Archive property set,
        /// and sets all other related properties needed for full functionality.
        /// Finally it saves the Local Upload file.
        /// If an exception occurs, this upload is marked with status = Failed,
        /// and the user has to delete it by clicking the delete button.
        /// </summary>
        /// <returns></returns>
        private async Task CreateNewUpload()
        {
            this.IsBusy = true;
            this.BusyMessage = "Creating upload...";

            try
            {
                this.Upload = await this._deepfreezeClient.InitiateUploadAsync(this.Archive);

                if (this.Upload != null)
                {
                    // store the upload url
                    this.LocalUpload.Url = this.Upload.Url;

                    // setup the s3 client.
                    this.SetS3Info(this.Upload.S3);
                    this.SetupS3Client(this.Upload.S3);

                    foreach (var info in this.LocalUpload.ArchiveFilesInfo)
                    {
                        if (this.Upload.S3.Prefix.StartsWith("/"))
                            info.KeyName = this.Upload.S3.Prefix.Remove(0, 1) + info.KeyName;
                        else
                            info.KeyName = this.Upload.S3.Prefix + info.KeyName;
                    }

                    // just for debug, doesn't hurt :)
                    NotifyOfPropertyChange(() => this.Upload);
                    NotifyOfPropertyChange(() => this.LocalUpload);

                    // finally save the local upload.
                    await this.SaveLocalUpload();

                    this.OperationStatus = Enumerations.Status.Paused;

                    // publish a message to automatically start uploading.
                    IUploadActionMessage uploadActionMessage = IoC.Get<IUploadActionMessage>();
                    uploadActionMessage.UploadAction = Enumerations.UploadAction.Start;
                    uploadActionMessage.UploadVM = this;
                    this._eventAggregator.PublishOnBackgroundThread(uploadActionMessage);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;

                if (this.Upload == null)
                {
                    // inform the user that she has to manually delete the archive
                    // from the website dashboard.
                    ErrorMessage += "\nError creating a new upload. Please delete the archive using the Deepfreeze website Dashboard.";
                }

                // for any reason, mark this upload with status failed
                // so the user can cancel it.
                this.OperationStatus = Enumerations.Status.Error;
                NotifyOfPropertyChange(() => this.Upload);
            }
            finally { IsBusy = false; }
        }

        /// <summary>
        /// Fetch online upload data, only for initializing
        /// on next application run or after clicking refresh button.
        /// This sets the UploadViewModel's Upload property.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private async Task FetchUploadAsync(string url)
        {
            try
            {
                this.Upload = await this._deepfreezeClient.GetUploadAsync(url).ConfigureAwait(false);

                if (this.Upload != null)
                {
                    this.SetS3Info(this.Upload.S3);
                    this.SetupS3Client(this.Upload.S3);
                }
            }
            catch (Exception e) { throw e; }
        }

        /// <summary>
        /// Fetch online archive data, only for initializing
        /// on next application run. This sets the UploadViewModel's Archive property.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private async Task FetchArchiveAsync(string archiveUrl)
        {
            try
            {
                this.Archive = await this._deepfreezeClient.GetArchiveAsync(archiveUrl).ConfigureAwait(false);
            }
            catch(Exception e) { throw e; }
        }

        /// <summary>
        /// Set S3 attributes when upload is loaded.
        /// </summary>
        private void SetS3Info(S3Info s3)
        {
            if (s3 != null)
            {
                this._s3Info = s3;
            }
        }

        /// <summary>
        /// Set DeepfreezeS3Client's amazon s3 client credentials and session token.
        /// </summary>
        /// <param name="s3"></param>
        private void SetupS3Client(S3Info s3)
        {
            this._s3Client.Setup(s3.TokenAccessKey, s3.TokenSecretKey, s3.TokenSession);
        }

        /// <summary>
        /// Abort all multi part uploads for this upload,
        /// for every file info with UploadId != null
        /// and IsUploaded = false.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> Abort()
        {
            try
            {
                var filesWithOpenUploads = this.LocalUpload.ArchiveFilesInfo
                   .Where(x => x.UploadId != null && !x.IsUploaded);

                await Task.Run(() =>
                    {
                        Parallel.ForEach(filesWithOpenUploads, async info =>
                        {
                            await this._s3Client.AbortMultiPartUploadAsync(this._s3Info.Bucket, info.KeyName, info.UploadId, CancellationToken.None)
                                .ConfigureAwait(false);
                        }
                        );
                    }
                ).ConfigureAwait(false);
                
                return true;
            }
            catch (Exception e) { throw e; }
        }

        /// <summary>
        /// Calculate the total uploaded size for all files that aren't uploaded yet
        /// but have some uploaded parts. 
        /// </summary>
        /// <returns></returns>
        private async Task<long> GetAllUploadedPartsSize()
        {
            try
            {
                long total = 0;
                List<Task<ListPartsResponse>> partsTasks = new List<Task<ListPartsResponse>>();

                // get archive file info with UploadId
                // so we count only those files that have already started uploading.
                var filesWithParts = this.LocalUpload.ArchiveFilesInfo
                    .Where(x => x.UploadId != null && !x.IsUploaded);

                foreach(var info in filesWithParts)
                {
                    var t = this._s3Client.ListPartsAsync(this._s3Info.Bucket, info.KeyName, info.UploadId, CancellationToken.None);
                    partsTasks.Add(t);
                }

                var taskResult = (await Task<ListPartsResponse>.WhenAll(partsTasks).ConfigureAwait(false)).ToList();

                foreach(var task in taskResult)
                {
                    total += task.Parts.Sum(x => x.Size);
                }

                return total;
            }
            catch(Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Calculate the sum of all uploaded files for this upload.
        /// </summary>
        /// <returns></returns>
        private long GetAllUploadedFilesSize()
        {
            long total = this.LocalUpload.ArchiveFilesInfo
                .Where(x => x.IsUploaded).Sum(x => x.Size);

            return total;
        }

        /// <summary>
        /// Calculate the total uploaded size of all completed files. If a file
        /// is not yet uploaded but has some parts uploaded, their size is also calculated
        /// and added in the final result.
        /// </summary>
        /// <returns></returns>
        private async Task CalculateTotalUploadedSize()
        {
            // calculate total uploaded size.
            var completedFilesSize = this.GetAllUploadedFilesSize();
            var completedPartsSize = await this.GetAllUploadedPartsSize().ConfigureAwait(false);

            this._totalProgress = completedFilesSize + completedPartsSize;
            this.Progress = this._totalProgress;
        }

        /// <summary>
        /// Save Local Upload to file.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> SaveLocalUpload()
        {
            try
            {
                // Save the newLocalUpload to the correct local upload file
                // in %APPDATA\Deepfreeze\uploads\ArchiveKey.json
                this.LocalUpload.SavePath = Path.Combine(Properties.Settings.Default.UploadsFolderPath, this.Archive.Key + ".json");

                _log.Info("Saving \"" + this.LocalUpload.SavePath + "\".");
                
                await Task.Run(() => LocalStorage.WriteJson(this.LocalUpload.SavePath, this.LocalUpload, Encoding.UTF8))
                    .ConfigureAwait(false);

                return true;
            }
            catch (Exception e) 
            {
                _log.Error("Error saving file \"" + this.LocalUpload.SavePath + "\", thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\".");
                throw e; 
            }
        }

        /// <summary>
        /// Delete Local Upload file.
        /// </summary>
        /// <returns></returns>
        private bool DeleteLocalUpload()
        {
            try
            {
                _log.Info("Deleting \"" + this.LocalUpload.SavePath + "\".");

                // Delete the LocalUpload file
                File.Delete(this.LocalUpload.SavePath);

                return true;
            }
            catch (Exception e)
            {
                _log.Error("Error deleting file \"" + this.LocalUpload.SavePath + "\", thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\".");
                throw e; 
            }
        }

        /// <summary>
        /// Prepare the upload view model but fetching it's online resources.
        /// </summary>
        /// <returns></returns>
        private async Task PrepareUploadAsync()
        {
            this.IsBusy = true;

            if (this._isRefreshing)
                this.BusyMessage = "Refreshing upload...";
            else
                this.BusyMessage = "Preparing upload...";

            bool isExistingUpload = false;

            try
            {
                if (this.LocalUpload == null)
                    throw new Exception("No local upload file found.");

                if (this.Upload == null || this._isRefreshing)
                {
                    // if upload is null then this means that this is
                    // an existing upload initializing after a new application
                    // instance run. So mark it as an existing upload,
                    // which will be used to automatically start the upload
                    // in case it is a new upload.
                    isExistingUpload = true;

                    await this.FetchUploadAsync(this.LocalUpload.Url).ConfigureAwait(false);
                }

                if (this.Archive == null || this._isRefreshing)
                    await this.FetchArchiveAsync(this.Upload.ArchiveUrl).ConfigureAwait(false);

                if (isExistingUpload)
                {
                    if (this.Upload.Status == Enumerations.Status.Pending)
                        this.OperationStatus = Enumerations.Status.Paused;
                    else
                        this.OperationStatus = this.Upload.Status;

                    // calculate total uploaded size.
                    await CalculateTotalUploadedSize().ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                if (e is Exceptions.DfApiException)
                {
                    this.OperationStatus = Enumerations.Status.Error;

                    var response = (e as Exceptions.DfApiException).HttpResponse;
                    
                    this.ErrorMessage = "Error: " + response.StatusCode + "\n" + e.Message;
                }

                if (e is AmazonS3Exception)
                {
                    this.ErrorMessage = (e as AmazonS3Exception).Message + " Try starting/refreshing the upload again.";
                }
            }
            finally
            {
                this.IsBusy = false;
                this.BusyMessage = null;
            }
        }

        /// <summary>
        /// Check upload's token expiration and if it expires in the next 5 minutes,
        /// fetch the upload resource to read the updated S3 attribute.
        /// </summary>
        /// <returns></returns>
        private async Task RenewUploadTokenAsync()
        {
            try
            {
                // first check if token is close to expiring
                var tokenDuration = this._s3Info.TokenExpiration - DateTime.UtcNow;

                // if upload token expires in less than 5 minutes, then Fetch the online upload
                // object to read the updated S3 object.
                if (tokenDuration.TotalMinutes < 5)
                {
                    _log.Info("Checking for updated upload token since it expires at " + this._s3Info.TokenExpiration + "(less than 5 minutes).");

                    await this.FetchUploadAsync(this.Upload.Url).ConfigureAwait(false);
                }
            }
            catch (Exception e) { throw e; }
        }

        /// <summary>
        /// Clear essential objects for this view model.
        /// </summary>
        private void Reset()
        {
            this.LocalUpload = null;
            this.Archive = null;
            this.Upload = null;
        }

        #endregion

        #region message_handlers

        public async Task Handle(IUploadActionMessage message)
        {
            if (message != null 
                && message.UploadVM == this)
            {
                switch(message.UploadAction)
                {
                    case Enumerations.UploadAction.Create:
                        await this.CreateNewUpload();
                        break;
                    case Enumerations.UploadAction.Start:
                        await this.StartUpload();
                        break;
                    case Enumerations.UploadAction.Pause:
                        await this.PauseUpload(true);
                        break;
                }
            }
        }

        #endregion

        #region events

        private async void Tick(object sender, object e)
        {
            try
            {
                await this.RenewUploadTokenAsync().ConfigureAwait(false);

                if (this._currentUploadIsMultipart)
                    this._currentFileProgress = this._s3Client.MultipartUploadProgress.Sum(x => x.Value);
                else
                    this._currentFileProgress = this._s3Client.SingleUploadProgress;

                this.Progress = this._currentFileProgress + this._totalProgress;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error refreshing progress: " + ex.Message;
            }
        }

        protected override async void OnActivate()
        {
            base.OnActivate();

            this._eventAggregator.Subscribe(this);

            // Call PrepareUploadAsync in case this is an existing upload
            // PrepareUploadAsync handles it's exceptions
            // and updates the UI so there's no need to
            // use a try-catch block here.
            if (!String.IsNullOrEmpty(this.LocalUpload.Url))
                await this.PrepareUploadAsync().ConfigureAwait(false);

        }

        protected override async void OnDeactivate(bool close)
        {
            // Immediately send cancel to cancel any upload tasks
            // if the operation status is equal to uploading.
            // This check takes place inside PauseUpload method's body.
            await this.PauseUpload(true);

            this._eventAggregator.Unsubscribe(this);
            this.Reset();

            base.OnDeactivate(close);
        }

        #endregion
    }
}
