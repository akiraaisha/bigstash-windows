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
    public class ArchiveViewModel : PropertyChangedBase, IArchiveViewModel
    {
        #region fields

        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private bool _isReset = true;
        private bool _hasChosenFiles = false;
        private List<string> _paths = new List<string>();
        private string _errorSelectingFiles;
        private string _busyMessageText;
        private string _errorCreatingArchive;

        private string _archiveTitle;
        private long _archiveSize = 0;
        private string _archiveSizeText;

        private bool _isBusy = false;

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

        public async Task ChooseFolder()
        {
            // Clear list with paths.
            _paths.Clear();
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

                    _paths.Add(dir);

                    this._archiveSize = await this.PrepareArchivePathsAndSize(_paths);

                    ArchiveSizeText = Properties.Resources.TotalArchiveSizeText +
                    LongToSizeString.ConvertToString((double)this._archiveSize);

                    HasChosenFiles = true;
                }
                catch (Exception e)
                {
                    IsReset = true;
                    ErrorSelectingFiles = e.Message;
                }
                finally { IsBusy = false; }
            }
        }

        public void HandleDragEnter(DragEventArgs e)
        {
            // Allow only FileDrop drag and drop actions.
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
            }
        }

        public async Task HandleDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Clear list with paths.
                _paths.Clear();
                // Clear errors
                ErrorSelectingFiles = null;

                // get paths from drop action.
                _paths.AddRange((string[])e.Data.GetData(DataFormats.FileDrop));

                if (_paths.Count() > 0)
                {
                    IsReset = false;
                    IsBusy = true;
                    BusyMessageText = Properties.Resources.CalculatingTotalArchiveSizeText;

                    try
                    {
                        this._archiveSize = await this.PrepareArchivePathsAndSize(_paths);

                        ArchiveSizeText = Properties.Resources.TotalArchiveSizeText +
                            LongToSizeString.ConvertToString((double)this._archiveSize);

                        HasChosenFiles = true;
                    }
                    catch(Exception ex)
                    {
                        IsReset = true;
                        ErrorSelectingFiles = ex.Message;
                    }
                    finally { IsBusy = false; }
                }
            }
        }

        public bool CanUpload
        {
            get
            {
                return !String.IsNullOrEmpty(ArchiveTitle) &&
                       !String.IsNullOrWhiteSpace(ArchiveTitle) &&
                       this._archiveSize > 0;
            }
        }

        public async Task Upload()
        {
            HasChosenFiles = false;
            IsBusy = true;
            BusyMessageText = Properties.Resources.CreatingArchiveText;

            try
            {
                throw new Exception("Something went wrong!!!");
                // create a new archive using DF API.
                var archive = await this._deepfreezeClient.CreateArchiveAsync(this._archiveSize, ArchiveTitle);

                // create a new upload using DF API.

                // reset the view
                this.Cancel(); 
            }
            catch (Exception e)
            {
                HasChosenFiles = true;
                ErrorCreatingArchive = e.Message;
            }
            finally 
            {
                IsBusy = false;
            }
        }

        public void Cancel()
        {
            IsBusy = false;
            HasChosenFiles = false;
            _paths.Clear();
            ArchiveTitle = null;
            ArchiveSizeText = null;
            this._archiveSize = 0;
            ErrorSelectingFiles = null;
            ErrorCreatingArchive = null;
            IsReset = true;
        }

        #endregion

        #region private methods

        private async Task PrepareArchivePathsAndSize(string dir)
        {
            try
            {
                _paths.Add(dir);
                await PrepareArchivePathsAndSize(_paths);
            }
            catch (Exception e) { throw e; }
        }

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

                // for each selected directory, compute its size and add all its file paths
                // in _paths.
                foreach (var dir in directories)
                {
                    var dirFiles = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);

                    if (dirFiles.Count() > 0)
                    {
                        await Task.Run(() =>
                            {
                                foreach (var f in dirFiles)
                                {
                                    // calculate file size and add its path in _paths to upload.
                                    size += new FileInfo(f).Length;
                                    _paths.Add(f);

                                }
                            }
                        );
                    }
                }
                
                foreach(var file in files)
                {
                    size += new FileInfo(file).Length;
                    _paths.Add(file);
                }

                if (size > (this._deepfreezeClient.Settings.ActiveUser.Quota.Size - this._deepfreezeClient.Settings.ActiveUser.Quota.Used))
                    throw new Exception("Your remaining Deepfreeze storage is not sufficient for the size of this archive.\nConsider buying more storage.");

                return size;
            }
            catch (UnauthorizedAccessException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        #endregion
    }
}
