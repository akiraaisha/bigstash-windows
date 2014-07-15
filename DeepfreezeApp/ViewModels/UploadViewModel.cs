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

namespace DeepfreezeApp
{
    [Export(typeof(IUploadViewModel))]
    public class UploadViewModel : Screen, IUploadViewModel
    {
        #region fields
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private bool _isBusy = false;
        private bool _isUploading = false;
        private string _errorMessage;
        private long _progress = 0;
        private string _busyMessage;

        private Archive _archive;
        private Upload _upload;
        private LocalUpload _localUpload;

        private IList<ArchiveFileInfo> _pendingFilesInfo;
        private ArchiveFileInfo _currentFileInfo;

        private string _currentFileName;

        private DeepfreezeS3Client _s3Client = new DeepfreezeS3Client();
        private S3Info _s3Info = new S3Info();

        private CancellationTokenSource _cts;

        private string _totalSizeString;
        private Enumerations.Status _operationStatus;

        private readonly long MIN_FILE_SIZE_FOR_MULTI_PART_UPLOAD = 5 * 1024 * 1024;

        private DispatcherTimer _refreshTimer = new DispatcherTimer();
        #endregion

        #region constructor
        [ImportingConstructor]
        public UploadViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;

            this._refreshTimer.Tick += Tick;
            _refreshTimer.Interval = new TimeSpan(0, 0, 5);
            //_refreshTimer.IsEnabled = true;
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
                    sb.Append(this._totalSizeString);

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
                this._archive = value;
                NotifyOfPropertyChange(() => this.Archive);
                this._totalSizeString = LongToSizeString.ConvertToString(this._archive.Size);
            }
        }

        public Upload Upload
        {
            get { return this._upload; }
            set
            { 
                this._upload = value;
                NotifyOfPropertyChange(() => this.Upload);
                //SetS3Info();
                //SetupS3Client();
                SetS3Info(this.Upload.S3);
                SetupS3Client(this.Upload.S3);
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

        #region public_methods

        /// <summary>
        /// Create a new upload by sending a post to the upload url.
        /// This method assumes that the VM already has an Archive property set,
        /// and sets all other related properties needed for full functionality.
        /// Finally it saves the Local Upload file.
        /// If an exception occurs, this upload is marked with status = Failed,
        /// and the user has to delete it by clicking the delete button.
        /// </summary>
        /// <returns></returns>
        public async Task CreateNewUpload()
        {
            this.IsBusy = true;
            this.OperationStatus = Enumerations.Status.Creating;

            try
            {
                this.Upload = await this._deepfreezeClient.InitiateUploadAsync(this.Archive);

                if (this.Upload != null)
                {
                    // store the upload url
                    this.LocalUpload.Url = this.Upload.Url;

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
                    this.SaveLocalUpload();

                    this.OperationStatus = Enumerations.Status.Paused;
                }
            }
            catch(Exception ex)
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
                this.OperationStatus = Enumerations.Status.Failed;
                NotifyOfPropertyChange(() => this.Upload);
            }
            finally { IsBusy = false; }
        }

        #endregion

        #region action_methods


        public async Task Start()
        {
            this.IsBusy = false;
            this.OperationStatus = Enumerations.Status.Uploading;

            this._cts = new CancellationTokenSource();
            CancellationToken token = this._cts.Token;

            // skip files with IsUploaded = true entirely.
            var lstFilesToUpload = this.LocalUpload.ArchiveFilesInfo.Where(x => !x.IsUploaded).ToList();

            // Subscribe to progress event;
            //this._s3Client.ProgressChanged +=
            //    new EventHandler<StreamTransferProgressArgs>(_s3Client_OnProgressChanged);

            this._refreshTimer.Start();

            foreach (var info in lstFilesToUpload)
            {
                CurrentFileInfo = info;
                
                try
                {
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
                            await this._s3Client.InitiateMultipartUpload(this._s3Info.Bucket, info.KeyName, token);

                        info.UploadId = initResponse.UploadId;

                        this.SaveLocalUpload();
                    }

                    bool uploadFinished = false;

                    if (info.Size > MIN_FILE_SIZE_FOR_MULTI_PART_UPLOAD)
                    {
                        // If this file info has an UploadId and IsUploaded = false, then proceed with uploading the file.
                        uploadFinished = await this._s3Client.UploadFileAsync(isNewFileUpload, this._s3Info.Bucket, info, token);

                        if (uploadFinished)
                        {
                            // send a complete request to finish the s3 upload.
                            var completeResponse = await this._s3Client.CompleteMultipartUpload(this._s3Info.Bucket, info.KeyName, info.UploadId, token);
                        }
                    }
                    else
                        uploadFinished = await this._s3Client.UploadSingleFileAsync(this._s3Info.Bucket, info, token);

                    // fire tick event after each file completes to show timely-updated progres.
                    this.Tick(this, null);

                    info.IsUploaded = uploadFinished;
                    this.SaveLocalUpload();
                }
                catch (AggregateException ex)
                {
                    ex.Handle(e =>
                    {
                        return e is OperationCanceledException;
                    });

                    this._refreshTimer.Stop();

                    // handle the rest exception types here.
                    ErrorMessage = ex.Message;
                }
                finally 
                {
                    IsBusy = false; // in case the user clicked pause.
                }
            }

            this.Tick(this, null);
            this._refreshTimer.Stop();

            // Since all files are uploaded send a patch to upload url with status uploaded to complete it.
            this.Upload = await this._deepfreezeClient.FinishUploadAsync(this.Upload);

            this.OperationStatus = this.Upload.Status;
        }

        public void Pause()
        {
            if (this.OperationStatus == Enumerations.Status.Uploading)
            {
                IsBusy = true;
                this.OperationStatus = Enumerations.Status.Paused;

                this._refreshTimer.Stop();

                if (this._cts != null)
                    this._cts.Cancel();
            }
        }

        public async Task Delete()
        {
            try
            {
                IsBusy = true;
                this.Pause();

                if (this.Upload != null)
                {
                    var abortSuccess = await this.Abort();

                    var deleteSuccess = await this._deepfreezeClient.DeleteUploadAsync(this.Upload);
                }

                this.DeleteLocalUpload();

                var removeUploadMessage = IoC.Get<IRemoveUploadViewModel>();
                removeUploadMessage.UploadVMToRemove = this;
                this._eventAggregator.PublishOnCurrentThread(removeUploadMessage);
            }
            catch (Exception e)
            {
                this.ErrorMessage = e.Message;
            }
            finally
            { this.IsBusy = false; }
        }

        public async Task Refresh()
        {
            try
            {
                await this.PrepareUploadAsync();
            }
            catch(Exception e) { }
        }

        #endregion

        #region private_methods

        /// <summary>
        /// Fetch online upload data, only for initializing
        /// on next application run.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private async Task FetchUploadAsync(string url)
        {
            try
            {
                this.Upload = await this._deepfreezeClient.GetUploadAsync(url);
            }
            catch (Exception e) { throw e; }
        }

        /// <summary>
        /// Fetch online archive data, only for initializing
        /// on next application run.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private async Task FetchArchiveAsync(string archiveUrl)
        {
            try
            {
                this.Archive = await this._deepfreezeClient.GetArchiveAsync(archiveUrl);
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

        private void SetS3Info()
        {
            this._s3Info.Bucket = "tokas";
            this._s3Info.TokenAccessKey = "AKIAJTWDMVXTYHVUTGRQ";
            this._s3Info.TokenSecretKey = "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";
        }

        /// <summary>
        /// Set DeepfreezeS3Client attributes.
        /// </summary>
        private void SetupS3Client()
        {
            this._s3Client.Setup(this._s3Info.TokenAccessKey, this._s3Info.TokenSecretKey);
        }

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
                            await this._s3Client.AbortMultiPartUploadAsync(this._s3Info.Bucket, info.KeyName, info.UploadId, CancellationToken.None);
                        }
                        );
                    }
                );
                
                return true;
            }
            catch (AggregateException e) { throw e; }
        }

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

                var taskResult = (await Task<ListPartsResponse>.WhenAll(partsTasks)).ToList();

                foreach(var task in taskResult)
                {
                    total += task.Parts.Sum(x => x.Size);
                }

                return total;
            }
            catch(AggregateException e)
            {
                throw e;
            }
        }

        private long GetAllUploadedFilesSize()
        {
            long total = this.LocalUpload.ArchiveFilesInfo
                .Where(x => x.IsUploaded).Sum(x => x.Size);

            return total;
        }

        private async Task CalculateTotalUploadedSize()
        {
            // calculate total uploaded size.
            var completedFilesSize = this.GetAllUploadedFilesSize();
            var completedPartsSize = await this.GetAllUploadedPartsSize();

            this.Progress = completedFilesSize + completedPartsSize;
        }

        /// <summary>
        /// Save Local Upload to file.
        /// </summary>
        /// <returns></returns>
        private bool SaveLocalUpload()
        {
            try
            {
                // Save the newLocalUpload to the correct local upload file
                // in %APPDATA\Deepfreeze\uploads\ArchiveKey.json
                this.LocalUpload.SavePath = Path.Combine(Properties.Settings.Default.UploadsFolderPath, this.Archive.Key + ".json");
                LocalStorage.WriteJson(this.LocalUpload.SavePath, this.LocalUpload, Encoding.UTF8);

                return true;
            }
            catch (Exception e) { throw e; }
        }

        /// <summary>
        /// Delete Local Upload file.
        /// </summary>
        /// <returns></returns>
        private bool DeleteLocalUpload()
        {
            try
            {
                // Delete the LocalUpload file
                File.Delete(this.LocalUpload.SavePath);

                return true;
            }
            catch (Exception e) { throw e; }
        }

        private async Task PrepareUploadAsync()
        {
            this.IsBusy = true;
            this.BusyMessage = "Preparing upload...";

            bool isExistingUpload = false;

            try
            {
                if (this.LocalUpload == null)
                    throw new Exception("No local upload file found.");

                if (this.Upload == null)
                {
                    // if upload is null then this means that this is
                    // an existing upload initializing after a new application
                    // instance run. So mark it as an existing upload,
                    // which will be used to automatically start the upload
                    // in case it is a new upload.
                    isExistingUpload = true;

                    await this.FetchUploadAsync(this.LocalUpload.Url);
                }

                if (this.Archive == null)
                    await this.FetchArchiveAsync(this.Upload.ArchiveUrl);

                if (isExistingUpload)
                {
                    if (this.Upload.Status == Enumerations.Status.Pending)
                        this.OperationStatus = Enumerations.Status.Paused;
                    else
                        this.OperationStatus = this.Upload.Status;

                    // calculate total uploaded size.
                    var completedFilesSize = this.GetAllUploadedFilesSize();
                    var completedPartsSize = await this.GetAllUploadedPartsSize();

                    this.Progress = completedFilesSize + completedPartsSize;
                }
                else
                {
                    // if it's a new upload, start it automatically
                    // and let it do its job :)
                    await Start();
                }
            }
            catch (Exception e)
            {
                this.OperationStatus = Enumerations.Status.Failed;
                
                if (e is Exceptions.DfApiException)
                {
                    var response = (e as Exceptions.DfApiException).HttpResponse;
                    
                    switch(response.StatusCode)
                    {
                        case System.Net.HttpStatusCode.NotFound:
                            this.ErrorMessage = "Error: " + response.StatusCode + "\nThe upload does not exist on the server. If the archive still exists in your Deepfreeze Dashboard, please delete it manually.";
                            break;
                        default:
                            this.ErrorMessage = e.Message;
                            break;
                    }
                    
                }
            }
            finally
            {
                this.IsBusy = false;
                this.BusyMessage = null;
            }
        }

        private async Task RenewUploadToken()
        {
            try
            {
                // first check if token is close to expiring
                var tokenDuration = this._s3Info.TokenExpiration - DateTime.Now.ToUniversalTime();

                if (tokenDuration.TotalMinutes < 5)
                {
                    await this.FetchUploadAsync(this.Upload.Url);
                }
            }
            catch (Exception e) { throw e; }
        }

        #endregion

        #region events

        private async void Tick(object sender, object e)
        {
            try
            {
                await this.RenewUploadToken();

                await this.CalculateTotalUploadedSize();
            }
            catch (Exception ex)
            {
                this._refreshTimer.Stop();
                ErrorMessage = ex.Message;
            }
        }

        protected override async void OnActivate()
        {
            // PrepareUploadAsync handles it's exceptions
            // and updates the UI so there's no need to
            // use a try-catch block here.
            await this.PrepareUploadAsync();

            base.OnActivate();
        }

        #endregion
    }
}
