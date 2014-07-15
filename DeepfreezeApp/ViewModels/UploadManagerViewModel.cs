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
        IHandleWithTask<IInitiateUploadMessage>, IHandle<IRemoveUploadViewModel>
    {
        #region fields
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private bool _isBusy = true;
        private IList<LocalUpload> _localUploads = new List<LocalUpload>();
        private BindableCollection<UploadViewModel> _filteredUploads = new BindableCollection<UploadViewModel>();
        private BindableCollection<UploadViewModel> _allUploads = new BindableCollection<UploadViewModel>();
        private string _totalUploadsText;

        private string _searchTerm = "";

        #endregion

        [Import]
        public ExportFactory<IUploadViewModel> UploadVMFactory { get; set; }

        public IUploadViewModel CreateNewUploadVM()
        {
            return UploadVMFactory.CreateExport().Value;
        }

        #region constructor
        [ImportingConstructor]
        public UploadManagerViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;

            this._eventAggregator.Subscribe(this);
        }

        #endregion

        #region properties

        public bool IsBusy
        {
            get { return this._isBusy; }
            set { this._isBusy = value; NotifyOfPropertyChange(() => IsBusy); }
        }

        public BindableCollection<UploadViewModel> FilteredUploads
        {
            get { return this._filteredUploads; }
            set { this._filteredUploads = value; NotifyOfPropertyChange(() => FilteredUploads); }
        }

        public string TotalUploadsText
        {
            get
            {
                if (IsBusy)
                    return "Preparing Uploads.";

                if (this.FilteredUploads != null && this.FilteredUploads.Count > 0)
                    return Properties.Resources.TotalUploadsText + FilteredUploads.Count.ToString();
                else
                    return Properties.Resources.NoUploadsHeaderText;
            }
            //get { return this._totalUploadsText; }
            //set { this._totalUploadsText = value; NotifyOfPropertyChange(() => TotalUploadsText); }
        }

        public string SearchTerm
        {
            get { return this._searchTerm; }
            set { this._searchTerm = value; NotifyOfPropertyChange(() => SearchTerm); }
        }

        #endregion

        #region action methods
        //public void FilterUploadsList()
        //{
        //    if (!String.IsNullOrEmpty(SearchTerm))
        //    {
        //        var filteredUploads = this._allUploads.Where(x => x.Archive.Title.Contains(SearchTerm))
        //            .OrderByDescending(x => x.Archive.Title).ToList();

        //        foreach(var u in filteredUploads)
        //        {
        //            FilteredUploads.Add(u);
        //        }
        //    }
        //}
        #endregion

        #region message handlers
        #endregion

        #region private methods

        /// <summary>
        /// Read local uploads files and populate the LocalUploads List,
        /// to keep them all in memory while this client runs.
        /// </summary>
        /// <returns></returns>
        private IList<LocalUpload> ReadLocalUploads()
        {
            try
            {
                IList<LocalUpload> localUploads = new List<LocalUpload>();
                var uploadFilesPaths = Directory.GetFiles(Properties.Settings.Default.UploadsFolderPath, "*", SearchOption.TopDirectoryOnly);

                foreach(var uploadFilePath in uploadFilesPaths)
                {
                    var content = File.ReadAllText(uploadFilePath, Encoding.UTF8);

                    if (content != null)
                    {
                        var localUpload = JsonConvert.DeserializeObject<LocalUpload>(content);
                        localUpload.SavePath = uploadFilePath;
                        localUploads.Add(localUpload);
                    }
                }

                return localUploads.OrderByDescending(x => x.SavePath).ToList();
            }
            catch (FileNotFoundException e) { throw e; }
            catch (JsonReaderException e) { throw e; }
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

                UploadViewModel u = CreateNewUploadVM() as UploadViewModel; // IoC.Get<IUploadViewModel>() as UploadViewModel;
                u.LocalUpload = localUpload;

                this.ActivateItem(u);

                this._allUploads.Add(u);
            }

            FilteredUploads = this._allUploads;
        }

        /// <summary>
        /// Initiate a new upload operation by creating a new UploadViewModel, setting its properties,
        /// saving its local upload file and finally automatically starting the upload process.
        /// </summary>
        /// <param name="newLocalUpload"></param>
        /// <param name="newArchive"></param>
        /// <returns></returns>
        private async Task InitiateNewUpload(Archive newArchive, IList<ArchiveFileInfo> archiveFilesInfo)
        {
            // create a new UploadViewModel.
            UploadViewModel newUploadVM = IoC.Get<IUploadViewModel>() as UploadViewModel;

            // add the new UploadViewModel in Uploads list.
            this._allUploads.Add(newUploadVM);
            FilteredUploads.Add(newUploadVM);
            NotifyOfPropertyChange(() => TotalUploadsText);

            // create a new local upload
            var newLocalUpload = new LocalUpload()
            {
                ArchiveFilesInfo = archiveFilesInfo.ToList()
            };

            newUploadVM.Archive = newArchive;
            newUploadVM.LocalUpload = newLocalUpload;

            // initiate a new upload and set it to newUploadVM.Upload
            await newUploadVM.CreateNewUpload();

            this._localUploads.Add(newUploadVM.LocalUpload);

            // final step: automatically start uploading.
            await newUploadVM.StartUpload();
        }

        #endregion

        #region events

        #endregion

        #region message_handlers

        public async Task Handle(IInitiateUploadMessage message)
        {
            await this.InitiateNewUpload(message.Archive, message.ArchiveFilesInfo);
        }

        public void Handle(IRemoveUploadViewModel message)
        {
            this.FilteredUploads.Remove(message.UploadVMToRemove);
            message.UploadVMToRemove = null;
            NotifyOfPropertyChange(() => TotalUploadsText);
        }

        #endregion

        #region events

        protected override void OnActivate()
        {
            IsBusy = true;

            try
            {
                this._localUploads = this.ReadLocalUploads();

                if (this._localUploads != null)
                    this.CreateUploadViewModels(this._localUploads);

            }
            catch (Exception e)
            {

            }
            finally
            {
                IsBusy = false;

                NotifyOfPropertyChange(() => TotalUploadsText);
            }

            base.OnActivate();
        }

        #endregion
    }
}
