using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using Caliburn.Micro;
using DeepfreezeSDK;
using DeepfreezeModel;
using System.IO;
using Newtonsoft.Json;

namespace DeepfreezeApp
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
        private BindableCollection<UploadViewModel> _uploads = new BindableCollection<UploadViewModel>();
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

        public BindableCollection<UploadViewModel> Uploads
        {
            get { return this._uploads; }
            set { this._uploads = value; NotifyOfPropertyChange(() => Uploads); }
        }

        public string TotalUploadsText
        {
            get
            {
                if (IsBusy)
                    return "Preparing Uploads...";

                if (this.Uploads.Count == 0)
                    return Properties.Resources.NoUploadsHeaderText;
                else
                    return null;
            }
        }

        public string ErrorMessage
        {
            get { return this._errorMessage; }
            set { this._errorMessage = value; NotifyOfPropertyChange(() => this.ErrorMessage); }
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
                    await Task.Run(() => Directory.GetFiles(Properties.Settings.Default.UploadsFolderPath, "*", SearchOption.TopDirectoryOnly))
                    .ConfigureAwait(false);

                _log.Info("Found " + uploadFilesPaths.Count() + " local upload files.");

                foreach(var uploadFilePath in uploadFilesPaths)
                {
                    var content = File.ReadAllText(uploadFilePath, Encoding.UTF8);

                    if (content != null)
                    {
                        var localUpload = JsonConvert.DeserializeObject<LocalUpload>(content);

                        // load only those local uploads that use the currently used api endpoint.
                        // we can filter this because each local upload has the URL property,
                        // which contains the api endpoint.
                        // Also, update the SavePath property using the files' paths, because this value
                        // is not preserved in the local files' content.
                        if (localUpload.Url.Contains(this._deepfreezeClient.Settings.ApiEndpoint))
                        {
                            localUpload.SavePath = uploadFilePath;
                            localUploads.Add(localUpload);
                        }
                    }
                }

                _log.Info("Loaded " + localUploads.Count() + " local upload files.");

                return localUploads.OrderByDescending(x => x.SavePath).ToList();
            }
            catch (Exception e) 
            {
                _log.Error("ReadLocalUploads threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");
                throw e; 
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

                this.ActivateItem(u);

                this.Uploads.Add(u);
            }

            _log.Info("Created " + this.Uploads.Count + " UploadViewModels for the upload manager.");
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
                ArchiveFilesInfo = archiveFilesInfo.ToList()
            };

            newUploadVM.Archive = newArchive;
            newUploadVM.LocalUpload = newLocalUpload;

            this._localUploads.Add(newUploadVM.LocalUpload);

            // add the new UploadViewModel at the beginning of the Uploads list.
            Uploads.Insert(0, newUploadVM);

            NotifyOfPropertyChange(() => TotalUploadsText);

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
            this._uploads.Clear();
            this.Uploads.Clear();
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
            if (this.Uploads.Contains(message.UploadVMToRemove))
            {
                lock (_removeLock)
                {
                    this.Uploads.Remove(message.UploadVMToRemove);
                    this.CloseItem(message.UploadVMToRemove);
                    message.UploadVMToRemove = null;
                    NotifyOfPropertyChange(() => TotalUploadsText);
                }
            }
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
                this.ErrorMessage = Properties.Resources.ErrorInitializingClientUploadsListGenericText;
            }
            finally
            {
                IsBusy = false;

                NotifyOfPropertyChange(() => TotalUploadsText);
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
