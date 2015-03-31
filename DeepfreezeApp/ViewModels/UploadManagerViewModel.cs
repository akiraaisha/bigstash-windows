using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using Caliburn.Micro;
using DeepfreezeSDK;
using DeepfreezeSDK.Exceptions;
using DeepfreezeModel;
using System.IO;
using Newtonsoft.Json;

namespace BigStash.WPF
{
    [Export(typeof(IUploadManagerViewModel))]
    public class UploadManagerViewModel : Conductor<Screen>.Collection.AllActive, IUploadManagerViewModel,
        IHandle<IInitiateUploadMessage>, IHandle<IRemoveUploadViewModelMessage>
    {
        #region fields

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(UploadManagerViewModel));
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private bool _isBusy = true;
        private IList<LocalUpload> _localUploads = new List<LocalUpload>();
        private BindableCollection<UploadViewModel> _pendingUploads = new BindableCollection<UploadViewModel>();
        private BindableCollection<UploadViewModel> _completedUploads = new BindableCollection<UploadViewModel>();
        private string _errorMessage;

        private static object _removeLock = new Object();

        #endregion

        #region UploadViewModelFactory
        [Import]
        public ExportFactory<IUploadViewModel> UploadVMFactory { get; set; }

        /// <summary>
        /// Create a new UploadViewModel viewmodel. This is used instead of the container
        /// because we need a new UploadViewModel each time a new upload occurs.
        /// </summary>
        /// <returns></returns>
        public IUploadViewModel CreateNewUploadVM()
        {
            _log.Info("Creating a new UploadViewModel for the upload manager.");
            return UploadVMFactory.CreateExport().Value;
        }

        #endregion

        #region constructor
        [ImportingConstructor]
        public UploadManagerViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
        }

        #endregion

        #region properties

        public bool IsBusy
        {
            get { return this._isBusy; }
            set { this._isBusy = value; NotifyOfPropertyChange(() => IsBusy); }
        }

        public BindableCollection<UploadViewModel> PendingUploads
        {
            get { return this._pendingUploads; }
            set 
            { 
                this._pendingUploads = value; 
                NotifyOfPropertyChange(() => PendingUploads);
            }
        }

        public BindableCollection<UploadViewModel> CompletedUploads
        {
            get { return this._completedUploads; }
            set 
            { 
                this._completedUploads = value; 
                NotifyOfPropertyChange(() => CompletedUploads);
            }
        }

        public string TotalPendingUploadsText
        {
            get
            {
                if (IsBusy)
                    return null;

                if (this.PendingUploads.Count == 0)
                    return Properties.Resources.NoPendingUploadsHeaderText;
                else
                    return null;
            }
        }

        public string TotalCompletedUploadsText
        {
            get
            {
                if (IsBusy)
                    return null;

                if (this.CompletedUploads.Count == 0)
                    return Properties.Resources.NoCompletedUploadsHeaderText;
                else
                    return null;
            }
        }

        public string ErrorMessage
        {
            get { return this._errorMessage; }
            set { this._errorMessage = value; NotifyOfPropertyChange(() => this.ErrorMessage); }
        }

        public bool HasUploads
        {
            get
            {
                return (this.PendingUploads.Count > 0 || this.CompletedUploads.Count > 0);
            }
        }

        public bool HasCompletedUploads
        {
            get
            {
                return this.CompletedUploads.Count > 0;
            }
        }

        public string ClearAllButtonContent
        { get { return Properties.Resources.ClearAllButtonContent; } }

        public string ClearAllCompletedButtonHelpText
        { get { return Properties.Resources.ClearAllCompletedButtonHelpText; } }

        #endregion

        #region action_methods

        /// <summary>
        /// Clear the completed upload list. Subsequently use each upload's remove method, without sending a removal message
        /// (as is the remove method's normal functionality). Finally call Clear() on the CompletedUploads list.
        /// </summary>
        public void ClearAllCompletedUploads()
        {
            foreach(var completedUpload in this.CompletedUploads)
            {
                // Call each UploadViewModel's remove method without sending a remove message to the upload manager,
                // since it's the manager that sends the removal request.
                completedUpload.RemoveUpload(true);
            }

            this.CompletedUploads.Clear();
            NotifyOfPropertyChange(() => TotalCompletedUploadsText);
            NotifyOfPropertyChange(() => this.HasUploads);
            NotifyOfPropertyChange(() => this.HasCompletedUploads);
        }

        /// <summary>
        /// Pause all active uploads.
        /// </summary>
        /// <returns></returns>
        public async Task PauseAllActiveUploads()
        {
            foreach(var pendingUpload in this.PendingUploads)
            {
                if (pendingUpload.IsUploading)
                {
                    await pendingUpload.PauseUpload(true);
                }
            }

            // for all to pause before returning with 100 ms intervals between checks.
            while(this.PendingUploads.Where(x => x.IsUploading).Count() > 0)
            {
                // wait 100 ms before checking again.
                await Task.Delay(100);
            }

            return;
        }

        /// <summary>
        /// Deactivate and close all child UploadViewModel instances.
        /// Provide close parameter with true value to close them as well. 
        /// </summary>
        /// <returns></returns>
        public async Task DeactivateAllUploads(bool close = false)
        {
            while(this.Items.Count > 0)
            {
                this.DeactivateItem(this.Items.FirstOrDefault(), true);
            }

            var activeUploadViewModels = new List<UploadViewModel>();

            // get all active upload view models, pending or completed
            activeUploadViewModels.AddRange(this.PendingUploads);
            activeUploadViewModels.AddRange(this.CompletedUploads);

            // for all to deactivate before returning with 100 ms intervals between checks.
            while (activeUploadViewModels.Where(x => x.IsActive).Count() > 0)
            {
                await Task.Delay(100);
            }

            return;
        }

        #endregion

        #region private methods

        /// <summary>
        /// Read local uploads files and populate the LocalUploads List,
        /// to keep them all in memory while this client runs. This method is called 
        /// in the overriden OnActivate method.
        /// </summary>
        /// <returns></returns>
        private async Task<IList<LocalUpload>> ReadLocalUploads()
        {
            try
            {
                IList<LocalUpload> localUploads = new List<LocalUpload>();

                _log.Info("Reading local upload files in directory \"" + Properties.Settings.Default.UploadsFolderPath + "\".");

                if (!Directory.Exists(Properties.Settings.Default.UploadsFolderPath))
                    Directory.CreateDirectory(Properties.Settings.Default.UploadsFolderPath);

                var uploadFilesPaths = 
                    await Task.Run(() => Directory.GetFiles(Properties.Settings.Default.UploadsFolderPath, "*" + Properties.Settings.Default.BigStashJsonFormat, SearchOption.TopDirectoryOnly))
                    .ConfigureAwait(false);

                _log.Info("Found " + uploadFilesPaths.Count() + " local upload files.");

                foreach(var uploadFilePath in uploadFilesPaths)
                {
                    var isValid = true;

                    // Try reading and deserializing the local file.
                    var localUpload = this.TryReadAndDeserializeLocalUploadFile(uploadFilePath);

                    if (localUpload == null)
                    {
                        // Try finding a backup and load that if it exists.
                        if (File.Exists(uploadFilePath + ".bak"))
                        {
                            _log.Warn("Backup found for \"" + uploadFilePath + "\".");
                        }

                        localUpload = this.TryReadAndDeserializeLocalUploadFile(uploadFilePath + ".bak");
                    }

                    // if this fails once again, skip it.
                    if (localUpload == null)
                    {
                        isValid = false;
                        localUpload = new LocalUpload()
                        {
                            Progress = 0,
                            Status = Enumerations.Status.Corrupted.GetStringValue()
                        };
                    }

                    // if a local upload file has an empty url value then exclude the upload
                    // and create a log warning about excluding it.
                    if (isValid && String.IsNullOrEmpty(localUpload.Url))
                    {
                        _log.Warn("Local Upload saved at \"" + uploadFilePath + "\" has url=\"" + localUpload.Url + "\" and it will be excluded from the uploads list.");
                        continue;
                    }

                    // load only those local uploads that use the currently used api endpoint.
                    // we can filter this because each local upload has the URL property,
                    // which contains the api endpoint.
                    // Also, update the SavePath property using the files' paths, because this value
                    // is not preserved in the local files' content.
                    if (isValid && !localUpload.Url.Contains(this._deepfreezeClient.Settings.ApiEndpoint))
                    {
                        _log.Warn("Local Upload saved at \"" + uploadFilePath + "\" has a different endpoint than the one in use, skipping it.");
                        continue;
                    }

                    localUpload.SavePath = uploadFilePath;
                    localUploads.Add(localUpload);
                }

                _log.Info("Finally loaded " + localUploads.Count() + " local upload files.");

                return localUploads.OrderByDescending(x => x.SavePath).ToList();
            }
            catch (Exception e) 
            {
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);
                throw; 
            }
        }

        /// <summary>
        /// Try reading and deserializing a local upload json file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private LocalUpload TryReadAndDeserializeLocalUploadFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                LocalUpload localUpload = null;

                _log.Debug("Try reading the local json upload file: \"" + path + "\".");

                // Try reading the text from the local file.
                var content = File.ReadAllText(path, Encoding.UTF8);

                if (content == null)
                {
                    return null;
                }

                _log.Debug("Try deserializing the local json upload file: \"" + path + "\".");

                // Try deserializing the read text to a LocalUpload object.
                localUpload = JsonConvert.DeserializeObject<LocalUpload>(content);

                if (localUpload == null)
                {
                    var jsonException = new Newtonsoft.Json.JsonReaderException("Deserialization of the local json upload file: \"" + path + "\" returned null.");
                    throw new BigStashException("Deserialization error.", jsonException, ErrorType.Client);
                }
                return localUpload;
            }
            catch(Exception e)
            {
                _log.Warn("An exception was thrown while reading or deserializing the Local Upload saved at \"" + path + "\" and it will be excluded from the uploads list.");
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".", e);

                return null;
            }
        }

        /// <summary>
        /// Create UploadViewModels based on the LocalUpload List,
        /// which should already be populated with all local uploads
        /// started from this client. This method runs at each application start
        /// and only then. Its purpose is to populate the client's uploads list.
        /// </summary>
        /// <param name="localUploads"></param>
        /// <returns>Task</returns>
        private void CreateUploadViewModels(IList<LocalUpload> localUploads)
        {
            IList<Task<UploadViewModel>> tasks = new List<Task<UploadViewModel>>();

            foreach (LocalUpload localUpload in localUploads)
            {
                //tasks.Add(InstatiateUploadViewModel(localUpload));

                UploadViewModel u = CreateNewUploadVM() as UploadViewModel;
                u.LocalUpload = localUpload;

                if (String.IsNullOrEmpty(localUpload.Status))
                {
                    localUpload.Status = Enumerations.Status.Pending.GetStringValue();
                }

                this.ActivateItem(u);

                Enumerations.Status status = Enumerations.GetStatusFromString(localUpload.Status);

                if (status == Enumerations.Status.Completed)
                {
                    this.CompletedUploads.Add(u);
                }
                else
                {
                    this.PendingUploads.Add(u);
                }
            }

            _log.Info("Created " + this.PendingUploads.Count + " UploadViewModels for the upload manager.");
        }

        /// <summary>
        /// Initiate a new upload operation by creating a new UploadViewModel, setting its properties,
        /// saving its local upload file and finall publishing an UploadActionMessage with a UploadAction.Create
        /// property for the new UploadViewModel to handle.
        /// </summary>
        /// <param name="newLocalUpload"></param>
        /// <param name="newArchive"></param>
        /// <returns></returns>
        private void InitiateNewUpload(Archive newArchive, IList<ArchiveFileInfo> archiveFilesInfo)
        {
            _log.Info("Initiating a new upload in the upload manager.");

            // create a new UploadViewModel.
            UploadViewModel newUploadVM = this.CreateNewUploadVM() as UploadViewModel;

            // create a new local upload
            var newLocalUpload = new LocalUpload()
            {
                ArchiveFilesInfo = archiveFilesInfo.ToList(),
                IsArchiveManifestUploaded = false
            };

            newUploadVM.Archive = newArchive;
            newUploadVM.LocalUpload = newLocalUpload;

            this._localUploads.Add(newUploadVM.LocalUpload);

            // add the new UploadViewModel at the beginning of the Uploads list.
            PendingUploads.Insert(0, newUploadVM);

            NotifyOfPropertyChange(() => TotalPendingUploadsText);
            NotifyOfPropertyChange(() => this.HasUploads);

            // activate the new uploadviewmodel before sending the create message.
            this.ActivateItem(newUploadVM);

            IUploadActionMessage uploadActionMessage = IoC.Get<IUploadActionMessage>();
            uploadActionMessage.UploadAction = Enumerations.UploadAction.Create;
            uploadActionMessage.UploadVM = newUploadVM;

            this._eventAggregator.PublishOnBackgroundThread(uploadActionMessage);
        }

        /// <summary>
        /// Clear essential lists for this viewmodel.
        /// </summary>
        private void Reset()
        {
            this._localUploads.Clear();
            this._pendingUploads.Clear();
            this.PendingUploads.Clear();
            this._completedUploads.Clear();
            this.CompletedUploads.Clear();

            NotifyOfPropertyChange(() => this.HasUploads);
        }

        #endregion

        #region message_handlers

        /// <summary>
        /// Handle any InitiateUploadMessage to create a new UploadViewModel
        /// after the user creates a new Archive. Essentially, this calls the 
        /// InitiateNewUpload private method.
        /// </summary>
        /// <param name="message"></param>
        public void Handle(IInitiateUploadMessage message)
        {
            this.InitiateNewUpload(message.Archive, message.ArchiveFilesInfo);
        }

        /// <summary>
        /// Handle any RemoveUploadViewModelMessage. This removes the message's 
        /// UploadViewModel from the current Uploads list, closes it and finally it 
        /// sets it to null. After the removal, it publishes a new RefreshUserMessage
        /// for the UserViewModel to handle and update the User object.
        /// </summary>
        /// <param name="message"></param>
        public void Handle(IRemoveUploadViewModelMessage message)
        {
            if (this.PendingUploads.Contains(message.UploadVMToRemove))
            {
                lock (_removeLock)
                {
                    this.PendingUploads.Remove(message.UploadVMToRemove);
                }
            }
            else if (this.CompletedUploads.Contains(message.UploadVMToRemove))
            {
                lock (_removeLock)
                {
                    this.CompletedUploads.Remove(message.UploadVMToRemove);
                }
            }

            this.DeactivateItem(message.UploadVMToRemove, true);
            message.UploadVMToRemove = null;

            NotifyOfPropertyChange(() => TotalPendingUploadsText);
            NotifyOfPropertyChange(() => TotalCompletedUploadsText);
            NotifyOfPropertyChange(() => this.HasUploads);
            NotifyOfPropertyChange(() => this.HasCompletedUploads);
        }

        #endregion

        #region events

        protected override async void OnActivate()
        {
            base.OnActivate();

            IsBusy = true;

            this._eventAggregator.Subscribe(this);

            try
            {
                this._localUploads = await this.ReadLocalUploads();

                if (this._localUploads != null)
                    this.CreateUploadViewModels(this._localUploads);

            }
            catch (Exception e)
            {
                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");

                this.ErrorMessage = Properties.Resources.ErrorInitializingClientUploadsListGenericText;
            }
            finally
            {
                IsBusy = false;

                NotifyOfPropertyChange(() => TotalPendingUploadsText);
                NotifyOfPropertyChange(() => TotalCompletedUploadsText);
                NotifyOfPropertyChange(() => this.HasUploads);
            }
        }

        protected override void OnDeactivate(bool close)
        {
            this._eventAggregator.Unsubscribe(this);

            this.Reset();

            base.OnDeactivate(close);
        }

        #endregion
    }
}
