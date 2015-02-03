using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Net.Http;
using System.IO;

using Caliburn.Micro;

using DeepfreezeSDK;
using DeepfreezeSDK.Exceptions;
using DeepfreezeModel;

using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using System.Diagnostics;

namespace DeepfreezeApp
{
    [Export(typeof(IUploadViewModel))]
    public class UploadViewModel : Screen, IUploadViewModel, IHandleWithTask<IUploadActionMessage>,
        IHandleWithTask<IInternetConnectivityMessage>
    {
        #region fields
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(UploadViewModel));
        private static object lockObject = new Object();

        private const int INTERVAL_FOR_TOKEN_REFRESH = 1;
        private const int INTERVAL_FOR_FAST_COMPLETION_CHECK = 5;
        private const int ONE_MEGABYTE = 1024 * 1024;

        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;
        private readonly IUploadManagerViewModel _uploadManager;

        private bool _isBusy = false;
        private bool _isUploading = false;
        private bool _isRefreshing = false;
        //private bool _currentUploadIsMultipart = false;
        private string _errorMessage;
        private long _progress = 0;
        private string _busyMessage;

        private Archive _archive;
        private Upload _upload;
        private LocalUpload _localUpload;
        private string _currentFileName;

        private long _totalProgress = 0; // this is updated when a file completes upload and not while uploading.

        private DeepfreezeS3Client _s3Client = new DeepfreezeS3Client();
        private S3Info _s3Info = new S3Info();

        private CancellationTokenSource _cts;

        private Enumerations.Status _operationStatus;

        private const long MIN_FILE_SIZE_FOR_MULTI_PART_UPLOAD = 5 * 1024 * 1024;
        private static readonly int PROCESSOR_COUNT = Environment.ProcessorCount;

        private DispatcherTimer _refreshProgressTimer;

        private bool _fetchUploadTakingTooLong = false;
        private bool _isWaitingForCompletedStatus = false;

        #endregion

        #region constructor

        [ImportingConstructor]
        public UploadViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient, IUploadManagerViewModel uploadManager)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
            this._uploadManager = uploadManager;

            // get a new DispatcherTimer on the UI Thread.
            this._refreshProgressTimer = new DispatcherTimer(new TimeSpan(0, 1, 0), DispatcherPriority.Normal, Tick, Application.Current.Dispatcher);
            this._refreshProgressTimer.Stop();
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
                NotifyOfPropertyChange(() => this.ProgressTooltip);
            }
        }

        public string ProgressText
        {
            get 
            {
                if (this.Archive != null)
                {
                    double percentage = 0;

                    var sb = new StringBuilder();
                    sb.Append("  ");

                    if (this.Archive.Size == 0)
                    {
                        percentage = 100;
                    }
                    else
                    { 
                        percentage = ((double)this.Progress / this.Archive.Size) * 100; 
                    }

                    if (percentage == 0 || percentage == 100)
                    {
                        sb.Append(percentage);
                    }
                    else
                    {
                        sb.Append(Math.Round(percentage, 1, MidpointRounding.AwayFromZero).ToString("0.0"));
                    }
                    sb.Append(Properties.Resources.PercentageOfText);
                    sb.Append(LongToSizeString.ConvertToString(this.Archive.Size));

                    return sb.ToString();
                }
                else
                    return null;
            }
        }

        public string ProgressTooltip
        {
            get
            {
                if (this.Archive == null)
                {
                    return null;
                }

                var sb = new StringBuilder();

                var progress = LongToSizeString.ConvertToString(this.Progress);
                var total = LongToSizeString.ConvertToString(this.Archive.Size);
                sb.Append(progress + " out of " + total);
                return sb.ToString();
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

        public string CurrentFileName
        {
            get { return this._currentFileName; }
            set 
            {
                this._currentFileName = value;
                NotifyOfPropertyChange(() => this.CurrentFileName); 
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

        public string ResumeButtonContent
        { get { return Properties.Resources.ResumeButtonContent; } }

        public string PauseButtonContent
        { get { return Properties.Resources.PauseButtonContent; } }

        public string DeleteButtonContent
        { get { return Properties.Resources.DeleteButtonContent; } }

        public string RemoveButtonContent
        { get { return Properties.Resources.RemoveButtonContent; } }

        public string ResumeButtonTooltipText
        { 
            get 
            {
                if (this.IsInternetConnected)
                    return null;
                else
                    return Properties.Resources.ResumeButtonDisabledTooltipText; 
            } 
        }

        public string DeleteButtonTooltipText
        {
            get
            {
                if (this.IsInternetConnected)
                    return null;
                else
                    return Properties.Resources.DeleteButtonDisabledTooltipText;
            }
        }

        public bool IsInternetConnected
        {
            get { return this._deepfreezeClient.IsInternetConnected; }
        }

        public bool FetchUploadTakingTooLong
        {
            get { return this._fetchUploadTakingTooLong; }
            set
            {
                this._fetchUploadTakingTooLong = value;
                NotifyOfPropertyChange(() => this.FetchUploadTakingTooLong);
            }
        }

        public bool IsWaitingForCompletedStatus
        {
            get { return this._isWaitingForCompletedStatus; }
            set
            {
                this._isWaitingForCompletedStatus = value;
                NotifyOfPropertyChange(() => this.IsWaitingForCompletedStatus);
            }
        }

        public string FetchUploadTakingTooLongText
        { get { return Properties.Resources.FetchUploadTakingTooLongText; } }

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

            try
            {
                this.IsBusy = false;
                this.IsUploading = true;
                this.OperationStatus = Enumerations.Status.Uploading;
                this.ErrorMessage = null;

                if (!this._deepfreezeClient.IsInternetConnected)
                    throw new Exception("The upload can't start/resume without an active Internet connection.");

                this._cts = new CancellationTokenSource();
                CancellationToken token = this._cts.Token;

                _log.Info("Starting archive upload with title \"" + this.Archive.Title + "\".");

                // set UserPaused to false and save the local file
                this.LocalUpload.UserPaused = false;
                await this.SaveLocalUpload();

                // Create and upload the archive manifest.
                await this.UploadArchiveManifestAsync(token).ConfigureAwait(false);
                
                // refresh the s3 token if it's currently expired.
                // this takes care of cases when an upload was automatically paused 
                // because of an expired token exception.
                if (this.Upload.S3.TokenExpiration < DateTime.UtcNow)
                {
                    await this.FetchUploadAsync(this.Upload.Url);
                }

                // set timer interval to 5 seconds to catch progress updates
                this._refreshProgressTimer.Interval = new TimeSpan(0, INTERVAL_FOR_TOKEN_REFRESH, 0);
                this._refreshProgressTimer.Start();

                // skip files with IsUploaded = true entirely.
                var lstFilesToUpload = this.LocalUpload.ArchiveFilesInfo.Where(x => !x.IsUploaded);

                var skippedFilesNum = this.LocalUpload.ArchiveFilesInfo.Count - lstFilesToUpload.Count();
                if (skippedFilesNum > 0)
                {
                    _log.Info("Archive upload with title \"" + this.Archive.Title + "\", skipping " + skippedFilesNum +
                        " files since they are already uploaded.");
                }

                // Let's create our upload queues, split to size groups.
                // One group will contain all small files to upload as a single file, that is everything smaller than 512 KB. (1)
                // One group will contain all the files to upload either as a single or as a multipart file with sizes larger than 1 MB and less or equal to 10 MB. (2)
                // One group will contain all the files to upload either as a multipart file with sizes larger than 10 MB and less or equal to 20 MB. (2)
                // One group will contain all the large files to upload as a multipart file with sizes larger than 20 MB. (2)
                // (1): Uploads files in parallel, 10 or 20 files in parallel based on the system's core count.
                //      Limit is 10 for less than 4 cores or 20 for 4 or more cores.
                // (2): Uploads files in parallel, <PROCESSOR_COUNT> files in parallel.
                // (3): Uploads files in parallel, 2 files in parallel.
                // (4): Uploads files serially, 1 at a time.

                // Get the very small files, < 512 KB.
                var verySmallFileQueue = new Queue<ArchiveFileInfo>(lstFilesToUpload.
                                                                    Where(x => x.Size <= ONE_MEGABYTE / 2));

                var parallelLimit = (PROCESSOR_COUNT < 4) ? 10 : 20;
                // upload the very small files queue
                await UploadFilesQueueAsync(verySmallFileQueue, parallelLimit, token).ConfigureAwait(false);

                // Get the small files
                var smallFilesQueue = new Queue<ArchiveFileInfo>(lstFilesToUpload.
                                                                    Where(x => x.Size > (ONE_MEGABYTE / 2) && x.Size <= 2 * MIN_FILE_SIZE_FOR_MULTI_PART_UPLOAD));

                parallelLimit = (PROCESSOR_COUNT - 1) == 0 ? PROCESSOR_COUNT : PROCESSOR_COUNT - 1;
                // upload the small files queue
                await UploadFilesQueueAsync(smallFilesQueue, parallelLimit, token).ConfigureAwait(false);

                // Get the medium files
                var mediumFilesQueue = new Queue<ArchiveFileInfo>(lstFilesToUpload.
                                                                    Where(x => x.Size > 2 * MIN_FILE_SIZE_FOR_MULTI_PART_UPLOAD && x.Size <= 4 * MIN_FILE_SIZE_FOR_MULTI_PART_UPLOAD));

                // upload the medium files queue
                await UploadFilesQueueAsync(mediumFilesQueue, 2, token).ConfigureAwait(false);

                // Get the large files
                var largeFilesQueue = new Queue<ArchiveFileInfo>(lstFilesToUpload.
                                                                    Where(x => x.Size > 4 * MIN_FILE_SIZE_FOR_MULTI_PART_UPLOAD));

                // upload the large files queue
                await UploadFilesQueueAsync(largeFilesQueue, 1, token).ConfigureAwait(false);

                long totalProgress = this.LocalUpload.ArchiveFilesInfo.Sum(x => x.Progress);

                if (this.Progress < totalProgress)
                {
                    Application.Current.Dispatcher.Invoke(() => this.Progress = totalProgress);
                }

                token.ThrowIfCancellationRequested();

                // Since all files are uploaded send a patch to upload url with status uploaded to complete it.
                this.Upload = await this._deepfreezeClient.FinishUploadAsync(this.Upload).ConfigureAwait(false);

                _log.Info("Finished archive upload with title \"" + this.Archive.Title + "\".");

                this.OperationStatus = this.Upload.Status;
                this.LocalUpload.Status = this.Upload.Status.GetStringValue();
                
                // no need to try and move to completed, the timer tick handles it.
                //this.TryMoveSelfToCompleted();

                this._refreshProgressTimer.Stop();

                // set timer interval to 10 seconds to catch fast archive completion. On it's first tick it will change to 1 minute intervals.
                this._refreshProgressTimer.Interval = new TimeSpan(0, 0, INTERVAL_FOR_FAST_COMPLETION_CHECK);
                // and start the timer again.
                this._refreshProgressTimer.Start();

                this.IsWaitingForCompletedStatus = true;

                //var notification = IoC.Get<INotificationMessage>();
                //notification.Message = "Archive " + this.Archive.Key + " " + Properties.Resources.UploadedNotificationText;
                //this._eventAggregator.PublishOnBackgroundThread(notification);
            }
            catch (Exception e)
            {
                // if a pause occured because of application shutdown, there's a chance that the timer is null before getting here, so this check is needed.
                if (this._refreshProgressTimer != null)
                    this._refreshProgressTimer.Stop(); 

                this.OperationStatus = Enumerations.Status.Paused;
                hasException = true;

                if (!(e is TaskCanceledException || e is OperationCanceledException))
                {
                    this.ErrorMessage = Properties.Resources.ErrorUploadingGenericText;
                    this.OperationStatus = Enumerations.Status.Paused;
                }
            }

            // if the user paused, we have to update the total progress, as some parts probably got aborted before finishing.
            if (hasException)
            {
                // check again for timer stop
                // if a pause occured because of application shutdown, there's a chance that the timer is null before getting here, so this check is needed.
                if (this._refreshProgressTimer != null) 
                    this._refreshProgressTimer.Stop();
                
                // in case of an exception other than operation cancelled (which is thrown by the user's pause action,
                // make sure to actually send a cancel to all remaining uploading part tasks to abort them.
                if (this._cts != null && !this._cts.IsCancellationRequested)
                    await this.PauseUpload(true);

                // after pause update _totalProgress, to have correct value.
                // TEMPORARY: Do this only if internet connectivity is ON,
                // meaning: ignore it if the pause happened after connectivity loss.
                if (this._deepfreezeClient.IsInternetConnected)
                    await CalculateTotalUploadedSize();

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
        public async Task PauseUpload(bool isAutomatic)
        {
            if (!this.IsUploading)
            {
                return;
            }

            this.IsBusy = !isAutomatic; // show busy only if user paused.
            this.BusyMessage = "Pausing upload...";
            this.OperationStatus = Enumerations.Status.Paused;

            this._refreshProgressTimer.Stop();

            var originalAction = isAutomatic ? "(automatic pause)" : "(user clicked the Pause button)";
            _log.Info("Pausing " + originalAction + " archive upload with title \"" + this.Archive.Title + "\".");

            this.LocalUpload.UserPaused = !isAutomatic;

            if (this._cts != null)
            {
                await Task.Run(() =>
                {
                    if (this._cts != null)
                    {
                        this._cts.Cancel();
                    }
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
                this.ErrorMessage = null;

                if (!this._deepfreezeClient.IsInternetConnected)
                    throw new Exception("The upload can't be deleted without an active Internet connection.");

                _log.Info("Deleting (user clicked the Delete button) archive upload with title \"" + this.Archive.Title + "\".");

                this.IsBusy = true;
                this.BusyMessage = "Deleting upload...";

                // No need to pause since delete is shown only in paused pending uploads.
                // await this.PauseUpload(true);

                if (this.Upload != null && 
                    this.Upload.Status != Enumerations.Status.Completed &&
                    this.Upload.Status != Enumerations.Status.Uploaded)
                {
                    try
                    {
                        var abortSuccess = await this.Abort().ConfigureAwait(false);
                    }
                    catch(AmazonS3Exception ae)
                    {
                        // If there's an aws exception when calling abort, just go on with the delete call to the df api.
                        _log.Error("Error while aborting S3 upload, thrown " + ae.GetType().ToString() + " with message \"" + ae.Message + "\".", ae);
                    }

                    var deleteSuccess = await this._deepfreezeClient.DeleteUploadAsync(this.Upload).ConfigureAwait(false);
                }

                this.DeleteLocalUpload();

                var removeUploadMessage = IoC.Get<IRemoveUploadViewModelMessage>();
                removeUploadMessage.UploadVMToRemove = this;
                this._eventAggregator.PublishOnBackgroundThread(removeUploadMessage);

                // send a message to refresh user storage stats.
                this._eventAggregator.PublishOnCurrentThread(IoC.Get<IRefreshUserMessage>());
            }
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);

                this.ErrorMessage += Properties.Resources.ErrorDeletingUploadGenericText;
            }
            finally
            { this.IsBusy = false; }
        }

        public void RemoveUpload(bool skipRemoveMessage = false)
        {
            try
            {
                if (this.Archive == null)
                    _log.Info("Removing already deleted upload on the server.");
                else
                    _log.Info("Removing (user clicked the Remove button) completed archive upload with title \"" + this.Archive.Title + "\".");

                this.DeleteLocalUpload();

                if (!skipRemoveMessage)
                {
                    var removeUploadMessage = IoC.Get<IRemoveUploadViewModelMessage>();
                    removeUploadMessage.UploadVMToRemove = this;
                    this._eventAggregator.PublishOnBackgroundThread(removeUploadMessage);
                }
            }
            catch(Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);

                this.ErrorMessage = Properties.Resources.ErrorRemovingUploadGenericText;
            }
        }

        /// <summary>
        /// Open the archive's page in the user's default browser.
        /// </summary>
        public void OpenArchivePage()
        {
            if (!String.IsNullOrEmpty(this.Archive.Url))
            {
                var authority = new Uri(this.Archive.Url).Authority;
                Process.Start("https://" + authority + "/a/" + this.Archive.Key);
            }
        }

        #endregion

        #region private_methods

        /// <summary>
        /// Creates a task for uploading a file, single or multipart.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private Task CreateUploadFileTask(ArchiveFileInfo info, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            // CurrentFileInfo = info;
            //this._currentFileProgress = 0;
            var task = Task.Run(async () =>
            {
                token.ThrowIfCancellationRequested();

                _log.Info("Start uploading file: \"" + info.FileName + "\".");

                // if UploadId is null then we mark this file info as a completely new S3 upload
                // else it's an upload started in the past.
                bool isNewFileUpload = (info.UploadId == null);

                if (!isNewFileUpload && info.LastModified < new FileInfo(info.FilePath).LastWriteTimeUtc)
                {
                    throw new Exception("The file " + info.FileName + " has changed since you selected it for archiving.\nCancel the upload and create a new archive.");
                }

                token.ThrowIfCancellationRequested();

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

                // this._refreshProgressTimer.Start();

                var currentUploadIsMultipart = info.Size > MIN_FILE_SIZE_FOR_MULTI_PART_UPLOAD;

                token.ThrowIfCancellationRequested();

                // Set the progress handler for the async upload methods.
                var progress = new Progress<Tuple<string, long>>();
                progress.ProgressChanged += (senderOfProgressChange, fileProgress) =>
                {
                    // update the total progress and each file's progress when this is not paused.
                    if (this.OperationStatus != Enumerations.Status.Paused)
                    {
                        // update the total progress if localupload is not null (for cases when this fires when shutting down the app).
                        if (this.LocalUpload != null)
                        {
                            info.Progress = fileProgress.Item2;

                            long newProgress = this.LocalUpload.ArchiveFilesInfo.Sum(x => x.Progress);

                            if (this.Progress <= newProgress)
                            {
                                this.Progress = newProgress;
                            }
                        }

                        // update the current file progress if upload is not null (for cases when this fires when shutting down the app).
                        if (info != null)
                        {
                            var totalFileProgress = (info.Size == 0) ? 100 : Math.Round(((double)info.Progress / info.Size) * 100, 1, MidpointRounding.AwayFromZero);
                            this.CurrentFileName = info.FileName + " (" + totalFileProgress + "%)";
                        }
                    }
                };

                if (currentUploadIsMultipart)
                {
                    // If this file info has an UploadId and IsUploaded = false, then proceed with uploading the file.
                    uploadFinished = await this._s3Client.UploadMultipartFileAsync(isNewFileUpload, this._s3Info.Bucket, info, this._cts, token, progress).ConfigureAwait(false);

                    token.ThrowIfCancellationRequested();

                    if (uploadFinished)
                    {
                        // this._refreshProgressTimer.Stop();

                        // send a complete request to finish the s3 upload.
                        var completeResponse = await this._s3Client.CompleteMultipartUploadAsync(this._s3Info.Bucket, info.KeyName, info.UploadId, token)
                            .ConfigureAwait(false);

                        // set the UploadId to null since it's completed and no longer exists.
                        info.UploadId = null;
                    }
                }
                else
                {
                    uploadFinished = await this._s3Client.UploadSingleFileAsync(this._s3Info.Bucket, info.KeyName, info.FilePath, token, progress).ConfigureAwait(false);
                    // this._refreshProgressTimer.Stop();
                }

                info.IsUploaded = uploadFinished;

                _log.Info("Finished uploading file: \"" + info.FileName + "\".");

                // progress now equals size since the file finished uploading
                info.Progress = info.Size;

                await this.SaveLocalUpload();
            }, token);

            return task;
        }

        /// <summary>
        /// Randomly inserts ArchiveFileInfo objects from a queue into a list of ArchiveFileInfo
        /// </summary>
        /// <param name="queue"></param>
        /// <param name="fileList"></param>
        /// <returns></returns>
        private IList<ArchiveFileInfo> InjectVerySmallFilesToLargerFilesList(Queue<ArchiveFileInfo> queue, IList<ArchiveFileInfo> fileList)
        {
            if (queue.Count == 0)
            {
                return fileList;
            }

            // if the list contains no files, add one.
            if (fileList.Count == 0)
            {
                fileList.Add(queue.Dequeue());
            }

            while (queue.Count > 0)
            {
                Random r = new Random(DateTime.Now.Millisecond);
                int randomIndex = r.Next(0, fileList.Count());
                fileList.Insert(randomIndex, queue.Dequeue());
            }

            return fileList;
        }

        /// <summary>
        /// Starts uploading a queue of upload tasks.
        /// </summary>
        /// <param name="filesToUploadQueue"></param>
        /// <param name="parallelLimit"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task UploadFilesQueueAsync(Queue<ArchiveFileInfo> filesToUploadQueue, int parallelLimit, CancellationToken token)
        {
            if (filesToUploadQueue.Count == 0)
            {
                return;
            }

            long sizeOfUploadsToStart = 0;
            var runningTasks = new List<Task>();
            Dictionary<Task, ArchiveFileInfo> taskToFileDict = new Dictionary<Task, ArchiveFileInfo>();

            // add at least one file to upload queue.
            var fileToUpload = filesToUploadQueue.Dequeue();
            // Add its size to the total size of uploads to start.
            sizeOfUploadsToStart += fileToUpload.Size;
            // Create an upload task.
            var taskToStart = this.CreateUploadFileTask(fileToUpload, token);
            // Add the newly created task in the list of tasks to start.
            runningTasks.Add(taskToStart);
            // Add the task and file in the taskToFileDict
            taskToFileDict.Add(taskToStart, fileToUpload);

            while (filesToUploadQueue.Count > 0 || runningTasks.Count > 0)
            {
                token.ThrowIfCancellationRequested();

                // populate runningTasks respecting the parallelLimit value.
                while (filesToUploadQueue.Count > 0 &&
                       runningTasks.Count < parallelLimit)
                {
                    token.ThrowIfCancellationRequested();

                    // Pop one out.
                    var nextFileToUpload = filesToUploadQueue.Dequeue();
                    // Add its size to the total size of uploads to start.
                    sizeOfUploadsToStart += nextFileToUpload.Size;
                    // Create an upload task.
                    var nextTaskToStart = this.CreateUploadFileTask(nextFileToUpload, token);
                    // Add the newly created task in the list of tasks to start.
                    runningTasks.Add(nextTaskToStart);
                    // Add the task and file in the taskToFileDict
                    taskToFileDict.Add(nextTaskToStart, nextFileToUpload);

                    if (runningTasks.Count >= parallelLimit)
                    { break; }
                }

                token.ThrowIfCancellationRequested();

                // Run parallel upload tasks.
                var finishedTask = await Task.WhenAny(runningTasks);

                // Subtrack the task's file's size from sizeOfUploadsToStart
                sizeOfUploadsToStart -= taskToFileDict[finishedTask].Size;

                // Remove the task from the taskToFileDictionary
                taskToFileDict.Remove(finishedTask);

                // Remove the finished task from the runningTasks list.
                runningTasks.Remove(finishedTask);

                // If the task faulted with an exception, clear the collections and throw the exception.
                if (finishedTask.Status == TaskStatus.Faulted)
                {
                    runningTasks.Clear();
                    filesToUploadQueue.Clear();

                    throw finishedTask.Exception;
                }

                // If the task got cancelled, then clear the collections.
                if (finishedTask.Status == TaskStatus.Canceled)
                {
                    runningTasks.Clear();
                    filesToUploadQueue.Clear();
                }

                // Mark the finished task for garbage collection.
                finishedTask = null;

                // Update the total upload progress.]
                // Use the UI Dispatcher to set the Progress property because this code runs in a background thread.
                long newProgress = this.LocalUpload.ArchiveFilesInfo.Sum(x => x.Progress);

                if (this.Progress < newProgress)
                {
                    Application.Current.Dispatcher.Invoke(() => this.Progress = newProgress);
                }
            }
        }

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
                    // store the upload status
                    this.LocalUpload.Status = this.Upload.Status.GetStringValue();

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
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\"." +
                           this.TryGetBigStashExceptionInformation(e), e);

                this.ErrorMessage = Properties.Resources.ErrorCreatingUploadGenericText;

                if (this.Upload == null)
                {
                    // inform the user that she has to manually delete the archive
                    // from the website dashboard.
                    ErrorMessage += "\n" + Properties.Resources.DeleteArchiveFromDashboardGenericText;
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
        private async Task FetchUploadAsync(string url, bool tryForever = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                // Add a timer with 2 minutes intervals to monitor slow upload fetches.
                // If the timer's tick event fires, then the DelayTick handler will update the UI to inform about the delay.
                // TODO: While this is already in place, there aren't any UI properties bound 
                // to the UploadViewModel.FetchUploadTakingTooLong property, so the user won't see any difference.
                // This is for future functionality.
                DispatcherTimer delayTimer = new DispatcherTimer(new TimeSpan(0, 2, 0), DispatcherPriority.Normal, DelayTick, Application.Current.Dispatcher);
                delayTimer.Start();

                this.Upload = await this._deepfreezeClient.GetUploadAsync(url, tryForever, cancellationToken).ConfigureAwait(false);

                delayTimer.Stop();
                delayTimer = null;

                if (this.Upload != null)
                {
                    this.LocalUpload.Status = this.Upload.Status.GetStringValue();
                    this.TryMoveSelfToCompleted();
                    this.SetS3Info(this.Upload.S3);
                    this.SetupS3Client(this.Upload.S3);
                }
            }
            catch (Exception) { throw; }
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
            catch(Exception) { throw; }
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
                // The code below tries to find pending s3 multipart uploads
                // and sends an abort request for each one found.
                // Currently the client doesn't support uploading many files in parallel,
                // that is only one file is uploaded per archive at any given time. So, files
                // are uploaded sequentially. As a result of this, the filesWithOpenUploads list
                // should always have only one member max. Regardless, the code is written in 
                // a way to support more than 1 aborts of not yet completed multipart uploads.

                // get all ArchiveFileInfo for each file to be aborted.
                var filesWithOpenUploads = this.LocalUpload.ArchiveFilesInfo
                   .Where(x => x.UploadId != null && !x.IsUploaded);

                // For each ArchiveFileInfo to be aborted, create an async Task making a call to the
                // _s3Client.AbortMultiPartUploadAsync() method and add it to the abortTasks list.
                var abortTasks = new List<Task>();
                foreach(var info in filesWithOpenUploads)
                {
                    var task = this._s3Client.AbortMultiPartUploadAsync(this._s3Info.Bucket, info.KeyName, info.UploadId, CancellationToken.None);

                    abortTasks.Add(task);
                }

                // Asynchronously wait for all the abort tasks to end.
                await Task.WhenAll(abortTasks).ConfigureAwait(false);
                
                return true;
            }
            catch (Exception) { throw; }
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
                List<Task<List<PartDetail>>> partsTasks = new List<Task<List<PartDetail>>>();

                // get archive file info with UploadId
                // so we count only those files that have already started uploading.
                var filesWithParts = this.LocalUpload.ArchiveFilesInfo
                    .Where(x => x.UploadId != null && !x.IsUploaded);

                foreach(var info in filesWithParts)
                {
                    var t = this._s3Client.ListPartsAsync(this._s3Info.Bucket, info.KeyName, info.UploadId, CancellationToken.None);
                    partsTasks.Add(t);
                }

                var taskResult = (await Task<List<PartDetail>>.WhenAll(partsTasks).ConfigureAwait(false)).ToList();

                foreach(var result in taskResult)
                {
                    total += result.Sum(x => x.Size);
                }

                return total;
            }
            catch(Exception)
            {
                throw;
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

            if (this.Progress < this._totalProgress)
                this.Progress = this._totalProgress;
        }

        /// <summary>
        /// Save Local Upload to file.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> SaveLocalUpload(bool useAsync = true)
        {
            if (this.IsBusy || 
                this.LocalUpload == null ||
                this.LocalUpload.Status == Enumerations.Status.Corrupted.GetStringValue() ||
                this.LocalUpload.Status == Enumerations.Status.NotFound.GetStringValue())
                return false;

            try
            {
                // Save the newLocalUpload to the correct local upload file
                // in %APPDATA\Deepfreeze\uploads\ArchiveKey.djf
                this.LocalUpload.SavePath = Path.Combine(Properties.Settings.Default.UploadsFolderPath, 
                    this.Archive.Key + Properties.Settings.Default.BigStashJsonFormat);

                _log.Info("Saving \"" + this.LocalUpload.SavePath + "\".");

                // get the latest progress value
                this.LocalUpload.Progress = this.Progress;

                if (useAsync)
                    await Task.Run(() => LocalStorage.WriteJson(this.LocalUpload.SavePath, this.LocalUpload, Encoding.UTF8))
                        .ConfigureAwait(false);
                else
                    LocalStorage.WriteJson(this.LocalUpload.SavePath, this.LocalUpload, Encoding.UTF8);

                return true;
            }
            catch (Exception e) 
            {
                _log.Error(Utilities.GetCallerName() + " error while saving file \"" + this.LocalUpload.SavePath + "\", thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);
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
                if (this.LocalUpload.SavePath != null)
                {
                    _log.Info("Deleting \"" + this.LocalUpload.SavePath + "\".");

                    // Delete the LocalUpload file
                    File.Delete(this.LocalUpload.SavePath);

                    // Take care of deleting the temp and/or backup file if it exists.
                    if (File.Exists(this.LocalUpload.SavePath + ".bak"))
                    {
                        File.Delete(this.LocalUpload.SavePath + ".bak");
                    }
                    if (File.Exists(this.LocalUpload.SavePath + ".tmp"))
                    {
                        File.Delete(this.LocalUpload.SavePath + ".tmp");
                    }
                }

                // Set this to null since the local file doesn't exist anymore.
                this.LocalUpload = null;

                return true;
            }
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " error while deleting file \"" + this.LocalUpload.SavePath + "\", thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);
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
                {
                    throw new Exception("No local upload file found");
                }

                if (this.LocalUpload.Status == Enumerations.Status.Corrupted.GetStringValue())
                {
                    throw new Exception(Properties.Resources.ErrorInitializingUploadLogText);
                }

                if (this.Upload == null || this._isRefreshing)
                {
                    // if upload is null then this means that this is
                    // an existing upload initializing after a new application
                    // instance run. So mark it as an existing upload,
                    // which will be used to automatically start the upload
                    // in case it is a new upload.
                    isExistingUpload = true;

                    await this.FetchUploadAsync(this.LocalUpload.Url, true).ConfigureAwait(false);
                }

                if (this.Archive == null || this._isRefreshing)
                    await this.FetchArchiveAsync(this.Upload.ArchiveUrl).ConfigureAwait(false);

                if (isExistingUpload)
                {
                    this.Progress = this.LocalUpload.Progress;

                    // calculate total uploaded size to keep track of the real uploaded size (completed files and completed parts).
                    await CalculateTotalUploadedSize().ConfigureAwait(false);

                    if (this.Upload.Status == Enumerations.Status.Pending)
                    {
                        this.OperationStatus = Enumerations.Status.Paused;

                        // check if this is a user paused upload or an automatically paused upload
                        // with pending status.
                        // in the second case, publish a message to handle automatic start.
                        if (!this.LocalUpload.UserPaused)
                        {
                            var uploadActionMessage = IoC.Get<IUploadActionMessage>();
                            uploadActionMessage.UploadAction = Enumerations.UploadAction.Start;
                            uploadActionMessage.UploadVM = this;
                            this._eventAggregator.PublishOnUIThread(uploadActionMessage);
                        }
                    }
                    else if (this.Upload.Status == Enumerations.Status.Uploaded)
                    {
                        // start polling the upload resource to catch the completed status change.
                        this._refreshProgressTimer.Interval = new TimeSpan(0, 1, 0);
                        this._refreshProgressTimer.Start();
                        this.OperationStatus = this.Upload.Status;
                        this.IsWaitingForCompletedStatus = true;
                    }
                    else
                    {
                        // if upload status is completed or error, just show the status.
                        this.OperationStatus = this.Upload.Status;
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " error while preparing an upload from file \"" + 
                           this.LocalUpload.SavePath + "\", thrown " + e.GetType().ToString() +
                           " with message \"" + e.Message + "\"." +
                           this.TryGetBigStashExceptionInformation(e), e);

                this.ErrorMessage = Properties.Resources.ErrorPreparingUploadGenericText;

                if (this.LocalUpload != null && this.Archive == null)
                {
                    if (this.LocalUpload.Status != Enumerations.Status.Corrupted.GetStringValue())
                    {
                        this.LocalUpload.Status = Enumerations.Status.NotFound.GetStringValue();
                    }

                    this.Archive = new Archive()
                    {
                        Size = 1,
                        Key = Path.GetFileNameWithoutExtension(this.LocalUpload.SavePath)
                    };

                    this.OperationStatus = Enumerations.Status.Error;
                }
                // if the archive doesn't exist on the server, simply remove the upload from the client.
                //if (this.LocalUpload != null && this.Upload != null && this.Archive == null)
                //{
                //    this.RemoveUpload();
                //}

                // this may get thrown when calculating the actual uploaded size to S3.
                if (e is AmazonS3Exception)
                {
                    this.OperationStatus = Enumerations.Status.Paused;
                    this.ErrorMessage = Properties.Resources.ErrorCalculatingS3UploadSizeText;
                }
            }
            finally
            {
                this.IsBusy = false;
                this.BusyMessage = null;
            }
        }

        /// <summary>
        /// Create the archive's manifest.
        /// </summary>
        /// <returns></returns>
        private ArchiveManifest CreateArchiveManifest()
        {
            _log.Debug("Creating the archive manifest.");

            ArchiveManifest archiveManifest = new ArchiveManifest();
            archiveManifest.ArchiveID = this.Archive.Key;
            archiveManifest.UserID = this._deepfreezeClient.Settings.ActiveUser.ID;

            string prefixToRemove;
            
            if (this.Upload.S3.Prefix.StartsWith("/"))
            {
                prefixToRemove = this.Upload.S3.Prefix.Remove(0, 1);
            }
            else
            {
                prefixToRemove = this.Upload.S3.Prefix;
            }

            foreach(var info in this.LocalUpload.ArchiveFilesInfo)
            {
                var fileManifest = new FileManifest()
                {
                    KeyName = info.KeyName.Replace(prefixToRemove, ""),
                    FilePath = info.FilePath,
                    Size = info.Size,
                    LastModified = info.LastModified,
                    MD5 = info.MD5
                };

                archiveManifest.Files.Add(fileManifest);
            }

            _log.Debug("Created the archive manifest.");

            return archiveManifest;
        }

        /// <summary>
        /// Upload the archive manifest to S3. This method includes creating the manifest file,
        /// as well as deleting it immediately after a successful upload. If the upload is unsuccessful,
        /// then an exception is thrown and so the upload gets paused. Manifest upload is a requirement in order
        /// to start uploading the archive's files.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task<bool> UploadArchiveManifestAsync(CancellationToken token)
        {
            string tempSavePath = String.Empty;

            try
            {
                // if the manifest has already been uploaded then skip this.
                if (this.LocalUpload.IsArchiveManifestUploaded)
                {
                    _log.Debug("Archive manifest has already been uploaded in a previous session.");
                    return true;
                }

                // Create and put to S3 the archive manifest before starting the upload.
                // The manifest has a keyname equal to the Upload.S3.Prefix + ".manifest".
                var archiveManifest = this.CreateArchiveManifest();

                StringBuilder manifestKeyNameSb = new StringBuilder();

                string prefix;

                if (this._s3Info.Prefix.StartsWith("/"))
                {
                    prefix = this._s3Info.Prefix.Remove(0, 1);
                }
                else
                {
                    prefix = this._s3Info.Prefix;
                }

                if (prefix.EndsWith("/"))
                {
                    // if prefix ends with / then remove it.
                    manifestKeyNameSb.Append(prefix.Remove(prefix.Length - 1, 1));
                }
                else
                {
                    manifestKeyNameSb.Append(prefix);
                }

                manifestKeyNameSb.Append(".manifest");

                // manifest temporary name is equal to the archive with .manifest suffix.
                tempSavePath = Path.Combine(Properties.Settings.Default.UploadsFolderPath,
                                                    this.Archive.Key + ".manifest");

                // Save the manifest file.
                await Task.Run(() =>
                {
                    _log.Debug("Creating the archive manifest file on disk.");
                    Utilities.CompressManifestToGZip(tempSavePath, archiveManifest);
                    _log.Debug("Created the archive manifest file on disk.");
                });

                if (!File.Exists(tempSavePath))
                {
                    throw new BigStashException("Error creating the zip file containing the archive manifest.");
                }

                // Upload the zip containing the manifest file to S3.
                var manifestUploaded = await this._s3Client.UploadSingleFileAsync(this._s3Info.Bucket, manifestKeyNameSb.ToString(), tempSavePath, token: token).ConfigureAwait(false);

                // Delete the manifest file.
                if (File.Exists(tempSavePath))
                {
                    _log.Debug("Deleting the archive manifest file.");
                    File.Delete(tempSavePath);
                    _log.Debug("Deleted the archive manifest file.");
                }

                // On successful upload, update the LocalUpload instance and local upload file about the manifest upload.
                if (manifestUploaded)
                {
                    _log.Debug("Archive manifest was successfully uploaded.");
                    this.LocalUpload.IsArchiveManifestUploaded = manifestUploaded;
                    await this.SaveLocalUpload();

                    return manifestUploaded;
                }

                // throw an exception if the manifest isn't uploaded
                // and the amazon s3 client didn't throw an exception.
                throw new BigStashException("Unsuccessful manifest upload.", DeepfreezeSDK.Exceptions.ErrorType.Client);
            }
            catch(Exception e)
            {
                // Delete the manifest file.
                if (File.Exists(tempSavePath))
                {
                    _log.Debug("Deleting the archive manifest file.");
                    File.Delete(tempSavePath);
                    _log.Debug("Deleted the archive manifest file.");
                }

                // Log the exception only if it's a BigStashException with no inner exception thrown above.
                // If the exception was thrown from the s3 client while trying to upload the manifest file,
                // then it's already logged.
                if (e is BigStashException && e.InnerException == null)
                {
                    _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\"." +
                               this.TryGetBigStashExceptionInformation(e), e);
                }

                throw;
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

            this._refreshProgressTimer.Tick -= Tick;
            this._refreshProgressTimer.Stop();
            this._refreshProgressTimer = null;
        }

        private void TryMoveSelfToCompleted()
        {
            // get the upload manager
            var manager = IoC.Get<IUploadManagerViewModel>() as UploadManagerViewModel;

            if (manager != null)
            {
                bool isInPending = manager.PendingUploads.Contains(this);
                bool isInCompleted = manager.CompletedUploads.Contains(this);

                if (this.Upload.Status == Enumerations.Status.Completed)
                {
                    if (isInPending)
                    {
                        lock (lockObject)
                        {
                            manager.PendingUploads.Remove(this);
                        }
                    }

                    if (!isInCompleted)
                    {
                        lock (lockObject)
                        {
                            manager.CompletedUploads.Add(this);
                        }
                    }

                    manager.NotifyOfPropertyChange(() => manager.TotalPendingUploadsText);
                    manager.NotifyOfPropertyChange(() => manager.TotalCompletedUploadsText);
                    manager.NotifyOfPropertyChange(() => manager.HasCompletedUploads);
                }
            }
        }

        /// <summary>
        /// Gets the request body of the sent request which resulted in the BigStashException throw.
        /// </summary>
        /// <param name="bgex"></param>
        /// <returns></returns>
        private string TryGetBigStashExceptionInformation(Exception ex)
        {
            if (ex is BigStashException)
            {
                var bgex = ex as BigStashException;
                var request = bgex.Request;

                if (request != null)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine();
                    sb.AppendLine("Error Type: " + bgex.ErrorType);
                    sb.AppendLine("Error Code: " + bgex.ErrorCode);
                    sb.AppendLine("Status Code: " + bgex.StatusCode);
                    sb.AppendLine("Failed Request:");
                    sb.Append("    " + request.ToString().Replace(Environment.NewLine, Environment.NewLine + "        "));

                    return sb.ToString();
                }

                return "";
            }

            return "";
        }

        #endregion

        #region message_handlers

        /// <summary>
        /// Handle upload action messages for automatic pause and resume functionality.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task Handle(IUploadActionMessage message)
        {
            if (message != null 
                && (message.UploadVM == this || message.UploadVM == null))
            {
                switch(message.UploadAction)
                {
                    case Enumerations.UploadAction.Create:
                        await this.CreateNewUpload();
                        break;
                    case Enumerations.UploadAction.Start:
                        if (this.Upload == null)
                            break;
                        else
                        {
                            // Don't start the upload if it was user paused or if status != pending.
                            if (!this.LocalUpload.UserPaused && this.Upload.Status == Enumerations.Status.Pending)
                                await this.StartUpload();

                            // Clear the ErrorMessage if it's about internet connectivity loss.
                            if (this.ErrorMessage != null && this.ErrorMessage.Contains(Properties.Resources.NoInternetConnectionMessage))
                                this.ErrorMessage = null;

                            break;
                        }
                    case Enumerations.UploadAction.Pause:
                        await this.PauseUpload(true);
                        break;
                }
            }
        }

        /// <summary>
        /// Handle upload action messages for automatic pause and resume functionality.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task Handle(IInternetConnectivityMessage message)
        {
            if (this.Upload == null)
                return;

            if (message != null)
            {
                // Notify the bound property IsInternetConnected to refresh
                // the 'Enabled' status of the 'Resume' and 'Delete' buttons.
                NotifyOfPropertyChange(() => this.IsInternetConnected);
                NotifyOfPropertyChange(() => this.ResumeButtonTooltipText);
                NotifyOfPropertyChange(() => this.DeleteButtonTooltipText);

                if (message.IsConnected &&
                    !this.LocalUpload.UserPaused && 
                    this.Upload.Status == Enumerations.Status.Pending)
                {
                    await this.StartUpload();
                }
                else
                {
                    await this.PauseUpload(true);
                }
            }
        }

        #endregion

        #region events

        private async void Tick(object sender, object e)
        {
            // When the timer is initialized in StartUpload to check for the completed status,
            // it's interval is set to 10 seconds to catch fast archive completions. So after it's first tick
            // change to interval to 1 minute to check in larger intervals.
            if (this._refreshProgressTimer.Interval.Seconds == INTERVAL_FOR_FAST_COMPLETION_CHECK &&
                this._refreshProgressTimer.Interval.Minutes == 0)
            {
                this._refreshProgressTimer.Stop();
                this._refreshProgressTimer.Interval = new TimeSpan(0, 0, 10);
                this._refreshProgressTimer.Start();
            }
            
            try
            {
                if (this.Upload.Status == Enumerations.Status.Pending
                    && this.IsUploading)
                {
                    await this.RenewUploadTokenAsync();
                }

                if (this.Upload.Status == Enumerations.Status.Uploaded
                    && this._deepfreezeClient.IsInternetConnected)
                {
                    await this.FetchUploadAsync(this.Upload.Url, true);

                    if (this.Upload.Status == Enumerations.Status.Completed ||
                        this.Upload.Status == Enumerations.Status.Error)
                    {
                        this.OperationStatus = this.Upload.Status;
                        this.IsWaitingForCompletedStatus = false;
                        this.LocalUpload.Status = this.Upload.Status.GetStringValue();
                        this._refreshProgressTimer.Stop();
                        this.ErrorMessage = null;

                        var notification = IoC.Get<INotificationMessage>();

                        bool isCompleted = this.Upload.Status == Enumerations.Status.Completed; // else status is error

                        if (isCompleted)
                        {
                            notification.Message = "Archive " + this.Archive.Key + " " + Properties.Resources.CompletedNotificationText;
                        }
                        else
                        {
                            notification.Message = Properties.Resources.StatusErrorNotificationText + " " + this.Archive.Key + ".";
                        }

                        this._eventAggregator.PublishOnBackgroundThread(notification);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(Utilities.GetCallerName() + " threw " + ex.GetType().ToString() + " with message \"" + ex.Message + "\"." +
                           this.TryGetBigStashExceptionInformation(ex), ex);

                this.ErrorMessage = Properties.Resources.ErrorRefreshingProgressGenericText;
            }
        }

        private void DelayTick(object sender, object e)
        {
            System.Action updateUIForDelay = () => this.FetchUploadTakingTooLong = true;
            Application.Current.Dispatcher.BeginInvoke(updateUIForDelay);
            ((DispatcherTimer)sender).Stop();
        }

        protected override async void OnActivate()
        {
            base.OnActivate();

            this._eventAggregator.Subscribe(this);

            // Call PrepareUploadAsync in case this is an existing upload
            // PrepareUploadAsync handles it's exceptions
            // and updates the UI so there's no need to
            // use a try-catch block here.
            if (!String.IsNullOrEmpty(this.LocalUpload.Url) ||
                this.LocalUpload.Status == Enumerations.Status.Corrupted.GetStringValue())
            {
                await this.PrepareUploadAsync().ConfigureAwait(false);
            }

        }

        protected override async void OnDeactivate(bool close)
        {
            // Immediately send cancel to cancel any upload tasks
            // if the operation status is equal to uploading.
            // This check takes place inside PauseUpload method's body.

            if (this.IsUploading)
            {
                var pauseTask = this.PauseUpload(true);
                await pauseTask;
            }

            //bool canRunTask = !(pauseTask.Status == TaskStatus.Canceled ||
            //                    pauseTask.Status == TaskStatus.Faulted ||
            //                    pauseTask.Status == TaskStatus.RanToCompletion);

            //if (canRunTask)
            //    pauseTask.RunSynchronously();

            var saveTask = this.SaveLocalUpload(false);
            await saveTask;

            //canRunTask = !(saveTask.Status == TaskStatus.Canceled ||
            //               saveTask.Status == TaskStatus.Faulted ||
            //               saveTask.Status == TaskStatus.RanToCompletion);

            //// do a final save
            //if (canRunTask)
            //    saveTask.RunSynchronously();

            this._eventAggregator.Unsubscribe(this);
            this.Reset();

            base.OnDeactivate(close);
        }

        #endregion
    }
}
