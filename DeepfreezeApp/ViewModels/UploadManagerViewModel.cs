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
    public class UploadManagerViewModel : PropertyChangedBase, IUploadManagerViewModel, IHandleWithTask<IFetchUploadsMessage>,
        IHandleWithTask<IInitiateUploadMessage>
    {
        #region fields
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private bool _isBusy = true;
        private LocalUploads _localUploads = new LocalUploads();
        private BindableCollection<UploadViewModel> _uploads = new BindableCollection<UploadViewModel>();
        private string _totalUploadsText;
        #endregion

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

        public BindableCollection<UploadViewModel> Uploads
        {
            get { return this._uploads; }
        }

        public string TotalUploadsText
        {
            get { return this._totalUploadsText; }
            set { this._totalUploadsText = value; NotifyOfPropertyChange(() => TotalUploadsText); }
        }

        #endregion

        #region action methods
        #endregion

        #region message handlers
        #endregion

        #region private methods

        private void SaveLocalUploads()
        {
            var urls = new LocalUploads()
            {
                Urls = Uploads.Select(x => x.Upload.Url).ToList()
            };

            LocalStorage.WriteJson(Properties.Settings.Default.UploadsFilePath, urls);
        }

        private LocalUploads LoadLocalUploads()
        {
            try
            {
                
                var content = File.ReadAllText(Properties.Settings.Default.UploadsFilePath);

                if (content != null)
                {
                    var localUploads = JsonConvert.DeserializeObject<LocalUploads>(content);
                    return localUploads;
                }
                else
                {
                    return null;
                }
            }
            catch (FileNotFoundException e) { throw e; } // do nothing, client has null settings.
            catch (JsonReaderException e) { throw e; } // do nothing, client has null settings.
        }

        private async Task CreateUploadViewModels(LocalUploads localUploads)
        {
            foreach (var url in localUploads.Urls)
            {
                UploadViewModel u = IoC.Get<IUploadViewModel>() as UploadViewModel;

                try
                {
                    u.Upload = await this._deepfreezeClient.GetUploadAsync(url);
                    Uploads.Add(u);
                    await u.GetArchive();
                }
                catch (Exception e) { throw e; }
            }
        }

        #endregion

        #region events

        #endregion

        public async Task Handle(IFetchUploadsMessage message)
        {
            try
            {
                this._localUploads = this.LoadLocalUploads();

                if (this._localUploads != null)
                    await this.CreateUploadViewModels(this._localUploads);

                TotalUploadsText = Properties.Resources.TotalUploadsText + Uploads.Count.ToString();
            }
            catch(Exception e)
            {
                TotalUploadsText = Properties.Resources.NoUploadsHeaderText;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task Handle(IInitiateUploadMessage message)
        {
            try
            {
                // add new upload to list of local upload urls
                if (this._localUploads.Urls == null)
                    this._localUploads.Urls = new List<string>();

                this._localUploads.Urls.Add(message.Archive.UploadUrl); 

                // create a new UploadViewModel and add it in Uploads.
                UploadViewModel newUploadVM = IoC.Get<IUploadViewModel>() as UploadViewModel;
                newUploadVM.Paths = message.Paths;

                // initiate a new upload
                newUploadVM.Upload = await this._deepfreezeClient.InitiateUploadAsync(message.Archive);

                if (newUploadVM != null)
                {
                    Uploads.Add(newUploadVM);

                    this.SaveLocalUploads();
                }
            }
            catch(Exception e)
            {

            }
        }
    }
}
