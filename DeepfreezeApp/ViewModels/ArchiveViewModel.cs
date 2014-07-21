using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows;

using Caliburn.Micro;
using DeepfreezeSDK;
using DeepfreezeModel;

namespace DeepfreezeApp
{
    [Export(typeof(IArchiveViewModel))]
    public class ArchiveViewModel : Screen, IArchiveViewModel
    {
        #region fields

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(ArchiveViewModel));
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;
        private static readonly long PART_SIZE = 5 * 1024 * 1024; // in bytes.
        private static readonly long MAX_ALLOWED_FILE_SIZE = PART_SIZE * 10000; // max parts is 10000, so max size is part size * 10000.

        private bool _isReset = true;
        private bool _hasChosenFiles = false;

        private string _errorSelectingFiles;
        private string _busyMessageText;
        private string _errorCreatingArchive;

        private string _archiveTitle;
        private long _archiveSize = 0;
        private string _archiveSizeText;

        private bool _isBusy = false;

        private List<ArchiveFileInfo> _archiveInfo = new List<ArchiveFileInfo>();

        private string _baseDirectory;

        #endregion

        #region constructor
        [ImportingConstructor]
        public ArchiveViewModel(IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
        }
        #endregion

        #region properties

        public string DragAndDropText
        { get { return Properties.Resources.DragAndDropText; } }

        public string ChooseFolderText
        { get { return Properties.Resources.ChooseFolderText; } }

        public string UploadButtonContent
        { get { return Properties.Resources.UploadButtonContent; } }

        public string CancelButtonContent
        { get { return Properties.Resources.CancelButtonContent; } }

        public string ArchiveTitleHelperText
        { get { return Properties.Resources.ArchiveTitleHelperText; } }

        public bool IsReset
        {
            get { return this._isReset; }
            set { this._isReset = value; NotifyOfPropertyChange(() => IsReset); }
        }

        public bool HasChosenFiles
        {
            get { return this._hasChosenFiles; }
            set { this._hasChosenFiles = value; NotifyOfPropertyChange(() => HasChosenFiles); }
        }

        public string ErrorSelectingFiles
        {
            get { return this._errorSelectingFiles; }
            set { this._errorSelectingFiles = value; NotifyOfPropertyChange(() => ErrorSelectingFiles); }
        }

        public string ErrorCreatingArchive
        {
            get { return this._errorCreatingArchive; }
            set { this._errorCreatingArchive = value; NotifyOfPropertyChange(() => ErrorCreatingArchive); }
        }
        
        public string ArchiveTitle
        {
            get { return this._archiveTitle; }
            set 
            { 
                this._archiveTitle = value; 
                NotifyOfPropertyChange(() => ArchiveTitle);
                NotifyOfPropertyChange(() => CanUpload);
            }
        }

        public string ArchiveSizeText
        {
            get { return this._archiveSizeText; }
            set 
            { 
                this._archiveSizeText = value; 
                NotifyOfPropertyChange(() => ArchiveSizeText);
                NotifyOfPropertyChange(() => CanUpload);
            }
        }

        public bool IsBusy
        {
            get { return this._isBusy; }
            set { this._isBusy = value; NotifyOfPropertyChange(() => IsBusy); }
        }

        public string BusyMessageText
        {
            get { return this._busyMessageText; }
            set { this._busyMessageText = value; NotifyOfPropertyChange(() => BusyMessageText); }
        }

        #endregion

        #region action methods

        /// <summary>
        /// Choose a folder for the new archive. This method opens a new Folder selection
        /// dialog.
        /// </summary>
        /// <returns></returns>
        public async Task ChooseFolder()
        {
            // Clear list with archive files info.
            this._archiveInfo.Clear();
            // Clear errors
            ErrorSelectingFiles = null;

            // Show the FolderBrowserDialog.
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.ShowNewFolderButton = false;

            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                IsReset = false;
                IsBusy = true;
                BusyMessageText = Properties.Resources.CalculatingTotalArchiveSizeText;

                try
                {
                    var dir = dialog.SelectedPath;
                    this._baseDirectory = Path.GetDirectoryName(dir) + "\\";

                    _log.Info("Selected directory \"" + dir + "\".");

                    var paths = new List<string>();
                    paths.Add(dir);

                    this._archiveSize = await this.PrepareArchivePathsAndSize(paths);

                    ArchiveSizeText = Properties.Resources.TotalArchiveSizeText +
                    LongToSizeString.ConvertToString((double)this._archiveSize);

                    HasChosenFiles = true;
                }
                catch (Exception e)
                {
                    _log.Error("Error preparing archive, thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\"");
                    IsReset = true;
                    ErrorSelectingFiles = e.Message;
                }
                finally { IsBusy = false; }
            }
        }

        /// <summary>
        /// Handle the DragEnter event from the user's drag and drop action. 
        /// If the DataFormats is not a FileDrop, then the drop is not allowed. 
        /// This way we make sure that only file drops are handled, so the archive creation can go on.
        /// </summary>
        /// <param name="e"></param>
        public void HandleDragEnter(DragEventArgs e)
        {
            // Allow only FileDrop drag and drop actions.
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handle the Drop event from the user's drag and drop action.
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task HandleDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Clear list with paths.
                this._archiveInfo.Clear();
                // Clear errors
                ErrorSelectingFiles = null;

                var paths = new List<string>();
                // get paths from drop action.
                paths.AddRange((string[])e.Data.GetData(DataFormats.FileDrop));

                if (paths.Count() > 0)
                {
                    IsReset = false;
                    IsBusy = true;
                    BusyMessageText = Properties.Resources.CalculatingTotalArchiveSizeText;

                    // get the base directory of the selection.
                    this._baseDirectory = Path.GetDirectoryName(paths.FirstOrDefault()) + "\\";

                    _log.Info("Drag and dropped files from directory \"" + this._baseDirectory + "\".");

                    try
                    {
                        this._archiveSize = await this.PrepareArchivePathsAndSize(paths);

                        ArchiveSizeText = Properties.Resources.TotalArchiveSizeText +
                            LongToSizeString.ConvertToString((double)this._archiveSize);

                        HasChosenFiles = true;
                    }
                    catch(Exception ex)
                    {
                        _log.Error("Error preparing archive, thrown " + ex.GetType().ToString() + " with message \"" + ex.Message + "\"");
                        IsReset = true;
                        ErrorSelectingFiles = ex.Message;
                    }
                    finally { IsBusy = false; }
                }
            }
        }

        /// <summary>
        /// Checks if the Upload button can be clicked or not.
        /// </summary>
        public bool CanUpload
        {
            get
            {
                return !String.IsNullOrEmpty(ArchiveTitle) &&
                       !String.IsNullOrWhiteSpace(ArchiveTitle) &&
                       this._archiveSize > 0;
            }
        }

        /// <summary>
        /// Initiate an Archive creation and publish an InitiateUploadMessage for
        /// the UploadManagerViewModel to handle.
        /// </summary>
        /// <returns></returns>
        public async Task Upload()
        {
            HasChosenFiles = false;
            IsBusy = true;
            BusyMessageText = Properties.Resources.CreatingArchiveText;

            try
            {
                _log.Info("Create new archive, size = " + this._archiveSize + " bytes, title = \"" + ArchiveTitle + "\".");
                // create a new archive using DF API.
                var archive = await this._deepfreezeClient.CreateArchiveAsync(this._archiveSize, ArchiveTitle);

                if (archive != null)
                {
                    // send a message to refresh user storage stats.
                    this._eventAggregator.PublishOnCurrentThread(IoC.Get<IRefreshUserMessage>());

                    // publish a message to UploadManager to initiate the upload.
                    var message = IoC.Get<IInitiateUploadMessage>();
                    message.Archive = archive;
                    message.ArchiveFilesInfo = this._archiveInfo.ToList();
                    this._eventAggregator.PublishOnBackgroundThread(message);
                }
                else
                    _log.Warn("CreateArchiveAsync returned null.");

                // reset the view
                this.Reset(); 
            }
            catch (Exception e)
            {
                HasChosenFiles = true;
                ErrorCreatingArchive = e.Message;
                _log.Error("Error creating archive (user clicked upload button), thrown " + e.GetType().ToString() + " with message \"" + e.Message + "\"");

                if (e is Exceptions.DfApiException)
                {
                    var response = (e as Exceptions.DfApiException).HttpResponse;
                    _log.Error("HTTP Response Status Code: " + response.StatusCode + ".");
                }
            }
            finally 
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Clear all essential properties for this viewmodel.
        /// </summary>
        public void Reset()
        {
            IsBusy = false;
            HasChosenFiles = false;
            ArchiveTitle = null;
            ArchiveSizeText = null;
            this._archiveSize = 0;
            ErrorSelectingFiles = null;
            ErrorCreatingArchive = null;
            this._archiveInfo.Clear();
            IsReset = true;
        }

        #endregion

        #region private methods

        /// <summary>
        /// Prepare the ArchiveFileInfo list needed for this archive. This method scans for all files
        /// to be included in the archive and prepares their keynames, file paths and base prefixes. 
        /// If the user selection doesn't include any files or if the user selection has a total size 
        /// more than the remaining free Deepfreeze Storage, this method throws an exception.
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        private async Task<long> PrepareArchivePathsAndSize(IEnumerable<string> paths)
        {
            try
            {
                long size = 0;

                // split paths to files and folders.
                var files = new List<string>();
                var directories = new List<string>();

                foreach(var p in paths)
                {
                    FileAttributes attr = File.GetAttributes(p);

                    if (attr.HasFlag(FileAttributes.Directory))
                        directories.Add(p);
                    else
                        files.Add(p);
                }

                // for each selected directory, add all files in _archiveFileInfo.
                foreach (var dir in directories)
                {
                    var dirFiles = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);

                    if (dirFiles.Count() > 0)
                    {
                        await Task.Run(() =>
                            {
                                foreach (var f in dirFiles)
                                {
                                    var info = new FileInfo(f);

                                    // Check that the archive size does not exceed the maximum allowed file size.
                                    // S3 supports multipart uploads with up to 10000 parts and 5 TB max size.
                                    // Since DF supports part size of 10 MB, archive size must not exceed 10 MB * 10000
                                    if (info.Length > MAX_ALLOWED_FILE_SIZE)
                                        throw new Exception("The file " + f + " exceeds the maximum allowed archive size of 100 GB.");

                                    var archiveFileInfo = new ArchiveFileInfo()
                                    {
                                        FileName = info.Name,
                                        KeyName = f.Replace(this._baseDirectory, "").Replace('\\', '/'),
                                        FilePath = f,
                                        Size = info.Length,
                                        LastModified = info.LastWriteTimeUtc,
                                        IsUploaded = false
                                    };

                                    this._archiveInfo.Add(archiveFileInfo);

                                    size += archiveFileInfo.Size;
                                }
                            }
                        );
                    }
                }
                
                // do the same for each individually selected files.
                foreach(var f in files)
                {
                    var info = new FileInfo(f);

                    // Check that the archive size does not exceed the maximum allowed file size.
                    // S3 supports multipart uploads with up to 10000 parts and 5 TB max size.
                    // Since DF supports part size of 5 MB, archive size must not exceed 5 MB * 10000
                    if (info.Length > MAX_ALLOWED_FILE_SIZE)
                        throw new Exception("The file " + info + " exceeds the maximum allowed archive size of " + 
                            LongToSizeString.ConvertToString((double)MAX_ALLOWED_FILE_SIZE) + ".");

                    var archiveFileInfo = new ArchiveFileInfo()
                    {
                        FileName = info.Name,
                        KeyName = info.Name,
                        FilePath = f,
                        Size = info.Length,
                        LastModified = info.LastWriteTimeUtc,
                        IsUploaded = false
                    };

                    this._archiveInfo.Add(archiveFileInfo);

                    size += archiveFileInfo.Size;
                }

                if (this._archiveInfo.Count == 0)
                    throw new Exception("Your selection doesn't contain any files. Nothing to upload.");

                // check that the archive size fits in user's DF storage.
                if (size > (this._deepfreezeClient.Settings.ActiveUser.Quota.Size - this._deepfreezeClient.Settings.ActiveUser.Quota.Used))
                    throw new Exception("Your remaining Deepfreeze storage is not sufficient for the size of this archive.\nConsider buying more storage.");

                // suggest an archive title
                if (directories.Count == 1)
                    this.ArchiveTitle = new DirectoryInfo(directories.First()).Name;
                else if (this._archiveInfo.Count == 1)
                    this.ArchiveTitle = this._archiveInfo.First().FileName;
                else
                    this.ArchiveTitle = "upload-" + String.Format("{0:yyyy-MM-dd}", DateTime.Now);

                return size;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        #endregion

        #region events

        protected override void OnActivate()
        {
            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            this.Reset();

            base.OnDeactivate(close);
        }

        #endregion
    }
}
