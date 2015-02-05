using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Diagnostics;

using Caliburn.Micro;
using DeepfreezeSDK;
using DeepfreezeModel;
using DeepfreezeSDK.Exceptions;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Windows.Threading;
using System.Threading;

namespace DeepfreezeApp
{
    [Export(typeof(IArchiveViewModel))]
    public class ArchiveViewModel : Screen, IArchiveViewModel, IHandle<IInternetConnectivityMessage>
    {
        #region fields

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(ArchiveViewModel));
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;
        private static readonly long PART_SIZE = 5 * 1024 * 1024; // in bytes.
        private static readonly long MAX_ALLOWED_FILE_SIZE = PART_SIZE * 10000; // max parts is 10000, so max size is part size * 10000.

        private bool _isReset = true;
        private bool _hasChosenFiles = false;
        private bool _hasInvalidFiles = false;

        private string _errorSelectingFiles;
        private string _busyMessageText;
        private string _errorCreatingArchive;

        private string _archiveTitle;
        private long _archiveSize = 0;
        private string _archiveSizeText;

        private bool _isBusy = false;

        private List<ArchiveFileInfo> _archiveInfo = new List<ArchiveFileInfo>();

        private Dictionary<string, Enumerations.FileCategory> _excludedFiles = new Dictionary<string, Enumerations.FileCategory>(StringComparer.OrdinalIgnoreCase);

        private string _baseDirectory;

        private string _totalFilesToArchiveText;
        private string _totalFilesToExcludeText;

        private string _excludedFilesText;

        private bool _isUserCancel = false;

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

        public string UploadButtonTooltipText
        {
            get
            {
                if (this.IsInternetConnected)
                    return Properties.Resources.UploadButtonEnabledTooltipText;
                else
                    return Properties.Resources.UploadButtonDisabledTooltipText;
            }
        }

        public bool IsInternetConnected
        {
            get { return this._deepfreezeClient.IsInternetConnected; }
        }

        public bool HasInvalidFiles
        {
            get { return this._hasInvalidFiles; }
            set { this._hasInvalidFiles = value; NotifyOfPropertyChange(() => this.HasInvalidFiles); }
        }

        public string TotalFilesToArchiveText
        {
            get { return this._totalFilesToArchiveText; }
            set { this._totalFilesToArchiveText = value; NotifyOfPropertyChange(() => this.TotalFilesToArchiveText); }
        }

        public string TotalFilesToExcludeText
        {
            get { return this._totalFilesToExcludeText; }
            set { this._totalFilesToExcludeText = value; NotifyOfPropertyChange(() => this.TotalFilesToExcludeText); }
        }

        public string ExcludedFilesText
        {
            get { return this._excludedFilesText; }
            set { this._excludedFilesText = value; NotifyOfPropertyChange(() => this.ExcludedFilesText); }
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

                    // get the base directory of the selection.
                    // if the path starts with 2 backslashes then it's a network location.
                    if (dir.StartsWith(@"\\"))
                    {
                        int lastIndexOfBackSlash = dir.LastIndexOf('\\');
                        string selectedDirName = dir.Substring(lastIndexOfBackSlash);
                        this._baseDirectory = dir.Replace(selectedDirName, "") + "\\";
                    }
                    else
                    {
                        this._baseDirectory = Path.GetDirectoryName(dir) + "\\";
                    }

                    _log.Info("Selected directory \"" + dir + "\".");

                    var paths = new List<string>();
                    paths.Add(dir);

                    this._archiveSize = await this.PrepareArchivePathsAndSize(paths);

                    this.SetTotalsTexts();

                    HasChosenFiles = true;
                }
                catch (Exception e)
                {
                    _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\"", e);

                    if (!this._isUserCancel)
                        this.ErrorSelectingFiles = Properties.Resources.ErrorChoosingFolderGenericText;

                    if (e is UnauthorizedAccessException)
                        this.ErrorSelectingFiles += " " + e.Message;

                    if (e.Message == Properties.Resources.ErrorNotEnoughSpaceGenericText)
                        this.ErrorSelectingFiles = e.Message;

                    // if the selection includes no files and a restricted folder WASN'T in the selection
                    // then show the no files to upload error message.
                    if (this._archiveInfo.Count == 0 && !this._isUserCancel)
                        this.ErrorSelectingFiles = e.Message;

                    this.IsReset = true;

                    if (this._isUserCancel)
                        this.Reset();
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

                    string parentDirOfSelection = paths.FirstOrDefault();

                    // get the base directory of the selection.
                    // if the path starts with 2 backslashes then it's a network location.
                    if (parentDirOfSelection.StartsWith(@"\\"))
                    {
                        int lastIndexOfBackSlash = parentDirOfSelection.LastIndexOf('\\');
                        string selectedDirName = parentDirOfSelection.Substring(lastIndexOfBackSlash);
                        this._baseDirectory = parentDirOfSelection.Replace(selectedDirName, "") + "\\";
                    }
                    else
                    {
                        var parentDirName = Path.GetDirectoryName(parentDirOfSelection);
                        if (String.IsNullOrEmpty(parentDirName))
                        {
                            this._baseDirectory = Path.GetPathRoot(parentDirOfSelection);
                        }
                        else
                        {
                            this._baseDirectory = Path.GetDirectoryName(paths.FirstOrDefault()) + "\\";
                        }
                    }
                    
                    _log.Info("Drag and dropped files from directory \"" + this._baseDirectory + "\".");

                    try
                    {
                        this._archiveSize = await this.PrepareArchivePathsAndSize(paths);

                        this.SetTotalsTexts();

                        HasChosenFiles = true;
                    }
                    catch (Exception ex)
                    {
                        _log.Error(Utilities.GetCallerName() + " threw " + ex.GetType().ToString() + " with message \"" + ex.Message + "\"", ex);

                        if (!this._isUserCancel)
                            this.ErrorSelectingFiles = Properties.Resources.ErrorAddingFilesGenericText;

                        if (ex is UnauthorizedAccessException)
                            this.ErrorSelectingFiles += " " + ex.Message;

                        if (ex.Message == Properties.Resources.ErrorNotEnoughSpaceGenericText)
                            this.ErrorSelectingFiles = ex.Message;

                        // if the selection includes no files and a restricted folder WASN'T in the selection
                        // then show the no files to upload error message.
                        if (this._archiveInfo.Count == 0 && !this._isUserCancel)
                            this.ErrorSelectingFiles = ex.Message;

                        this.IsReset = true;

                        if (this._isUserCancel)
                            this.Reset();
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
                       this._archiveInfo.Count > 0 &&
                       this.IsInternetConnected;
            }
        }

        /// <summary>
        /// Initiate an Archive creation and publish an InitiateUploadMessage for
        /// the UploadManagerViewModel to handle.
        /// </summary>
        /// <returns></returns>
        public async Task Upload()
        {
            try
            {
                if (!this._deepfreezeClient.IsInternetConnected)
                    throw new Exception("Can't upload an archive without an active Internet connection.");

                HasChosenFiles = false;
                IsBusy = true;
                BusyMessageText = Properties.Resources.CreatingArchiveText;

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

                _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\"." +
                           BigStashExceptionHelper.TryGetBigStashExceptionInformation(e), e);

                this.ErrorCreatingArchive = Properties.Resources.ErrorCreatingArchiveGenericText;
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
            this._excludedFiles.Clear();
            this.HasInvalidFiles = false;
            this.TotalFilesToExcludeText = null;
            this._isUserCancel = false;
            IsReset = true;
        }

        public async void ExportInvalidFilesList()
        {
            StringBuilder topMessage = new StringBuilder();
            StringBuilder bottomMessage = new StringBuilder();
            StringBuilder allFilePaths = new StringBuilder();

            foreach (var excludedFile in this._excludedFiles)
            {
                allFilePaths.AppendLine(excludedFile.Key + " (" + excludedFile.Value.GetStringValue() + ")");
            }

            topMessage.Append(Properties.Resources.FollowingFilesNotArchivedText);

            bottomMessage.Append(Properties.Resources.ClickSaveToExportFileListText); 

            var excludedFilesVM = IoC.Get<IExcludedFilesViewModel>() as ExcludedFilesViewModel;
            excludedFilesVM.ArchiveTitle = this.ArchiveTitle;
            excludedFilesVM.Title = Properties.Resources.ExcludedFilesTitle + "\"" + excludedFilesVM.ArchiveTitle + "\"";
            excludedFilesVM.TopMessageText = topMessage.ToString();
            excludedFilesVM.BottomMessageText = bottomMessage.ToString();
            excludedFilesVM.ExcludedFilesText = await Task.Run(() => allFilePaths.ToString());

            var windowManager = IoC.Get<IWindowManager>();
            await windowManager.ShowViewDialogAsync(excludedFilesVM);
        }

        public void OpenNameRulesFAQURL()
        {
            Process.Start(Properties.Settings.Default.BigStashNameRulesFAQURL);
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
                bool isDirectoryRestricted;

                foreach (var p in paths)
                {
                    FileAttributes attr = File.GetAttributes(p);

                    // if the selected folder is indeed a directory and not a junction point
                    // add it for archiving.
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory &&
                        (attr & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint)
                    {
                        isDirectoryRestricted = CheckIfDirectoryIsRestricted(p);

                        if (!isDirectoryRestricted)
                        {
                            directories.Add(p);
                        }
                        else
                        {
                            this._excludedFiles.Add(p, Enumerations.FileCategory.RestrictedDirectory);
                        }
                    }
                        
                    else
                        files.Add(p);
                }

                var subDirectories = new List<string>();

                // Get all subdirectories under the 1st selected directory, again ignoring any junction points.
                foreach (var dir in directories)
                {
                    var subsWithoutJunctions = await IgnoreJunctionsUnderPath(dir);
                    subDirectories.AddRange(subsWithoutJunctions.OrderBy(x => x));
                }

                // add all found subdirectories in the directories list
                // which will be scanned for files at the top level for each directory.
                if (subDirectories.Count > 0)
                    directories.AddRange(subDirectories);

                // for all directories selected for archiving, add all files in _archiveFileInfo.
                foreach (var dir in directories)
                {
                    // Fetch all files in directory, only those on the top level.
                    var dirFiles = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);

                    if (dirFiles.Count() > 0)
                    {
                        await Task.Run(() =>
                        {
                            foreach (var f in dirFiles)
                            {
                                var fileCategory = Utilities.CheckFileApiRestrictions(f);

                                if (fileCategory != Enumerations.FileCategory.Normal)
                                {
                                    this.HasInvalidFiles = true;
                                    this._excludedFiles.Add(f, fileCategory);
                                    continue;
                                }

                                var info = new FileInfo(f);

                                // Check that the archive size does not exceed the maximum allowed file size.
                                // S3 supports multipart uploads with up to 10000 parts and 5 TB max size.
                                // Since DF supports part size of 10 MB, archive size must not exceed 10 MB * 10000
                                if (info.Length > MAX_ALLOWED_FILE_SIZE)
                                    throw new Exception("The file " + f + " exceeds the maximum allowed archive size of " +
                                        LongToSizeString.ConvertToString((double)MAX_ALLOWED_FILE_SIZE) + ".");

                                var archiveFileInfo = new ArchiveFileInfo()
                                {
                                    FileName = info.Name,
                                    KeyName = f.Replace(this._baseDirectory, "").Replace('\\', '/'),
                                    FilePath = f,
                                    Size = info.Length,
                                    LastModified = info.LastWriteTimeUtc,
                                    MD5 = Utilities.GetMD5Hash(f),
                                    IsUploaded = false
                                };

                                this._archiveInfo.Add(archiveFileInfo);

                                size += archiveFileInfo.Size;
                            }
                        }
                        );
                    }
                }

                // Remove the subdirectories from the directories list, so only the initially selected
                // directory(ies) is left. We need to keep clean the directories list, so the code below
                // suggesting an upload name gets the corrent amount of initially selected directories.
                foreach(string subDir in subDirectories)
                {
                    directories.Remove(subDir);
                }

                // do the same for each individually selected files.
                foreach (var f in files)
                {
                    var fileCategory = Utilities.CheckFileApiRestrictions(f);

                    if (fileCategory != Enumerations.FileCategory.Normal)
                    {
                        this.HasInvalidFiles = true;
                        this._excludedFiles.Add(f, fileCategory);
                        continue;
                    }

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
                        MD5 = Utilities.GetMD5Hash(f),
                        IsUploaded = false
                    };

                    this._archiveInfo.Add(archiveFileInfo);

                    size += archiveFileInfo.Size;
                }

                var result = await ShowRestrictedWarningMessage();

                // if the user clicked cancel in the warning message box because of a restricted folder
                // then cancel the new archive archive creation and reset the ViewModel.
                if (result == MessageBoxResult.Cancel)
                {
                    this._isUserCancel = true;
                    throw new Exception("User cancelled because of a restricted folder");
                }

                if (this._archiveInfo.Count == 0)
                {
                    throw new Exception("Your selection doesn't contain any files. Nothing to upload.");
                }
                    
                // check that the archive size fits in user's DF storage.
                if (size > (this._deepfreezeClient.Settings.ActiveUser.Quota.Size - this._deepfreezeClient.Settings.ActiveUser.Quota.Used))
                {
                    // get the userviewmodel to refresh stats.
                    // we could use the messaging service, but we actually need to wait until the stats are refreshed
                    // before checking the sizes again. Sending a message is a fire and forget style, so that couldn't work here.
                    var userVM = IoC.Get<IUserViewModel>() as UserViewModel;
                    await userVM.RefreshUser();

                    if (size > (this._deepfreezeClient.Settings.ActiveUser.Quota.Size - this._deepfreezeClient.Settings.ActiveUser.Quota.Used))
                        throw new Exception(Properties.Resources.ErrorNotEnoughSpaceGenericText);
                }

                // suggest an archive title
                if (directories.Count == 1 && files.Count == 0)
                    this.ArchiveTitle = new DirectoryInfo(directories.First()).Name;
                else if (this._archiveInfo.Count == 1)
                    this.ArchiveTitle = this._archiveInfo.First().FileName;
                else
                    this.ArchiveTitle = "upload-" + String.Format("{0:yyyy-MM-dd}", DateTime.Now);

                return size;
            }
            catch (Exception e)
            {
                this._excludedFiles.Clear();
                throw e;
            }
        }

        private async Task<IList<string>> IgnoreJunctionsUnderPath(string root)
        {
            IList<string> allSubDirs = new List<string>();
            Stack<string> dirs = new Stack<string>();

            try
            {
                if (!System.IO.Directory.Exists(root))
                {
                    throw new ArgumentException();
                }
                dirs.Push(root);

                await Task.Run(() =>
                    {
                        while (dirs.Count > 0)
                        {
                            string currentDir = dirs.Pop();
                            string[] subDirs;
                            
                            try
                            {
                                subDirs = System.IO.Directory.GetDirectories(currentDir);
                            }
                            // An UnauthorizedAccessException exception will be thrown if we do not have 
                            // discovery permission on a folder or file. It may or may not be acceptable  
                            // to ignore the exception and continue enumerating the remaining files and  
                            // folders. It is also possible (but unlikely) that a DirectoryNotFound exception  
                            // will be raised. This will happen if currentDir has been deleted by 
                            // another application or thread after our call to Directory.Exists. The  
                            // choice of which exceptions to catch depends entirely on the specific task  
                            // you are intending to perform and also on how much you know with certainty  
                            // about the systems on which this code will run. 
                            catch (Exception e)
                            {
                                Console.WriteLine(e.Message);
                                throw e;
                            }

                            // Push the subdirectories onto the stack for traversal. 
                            // This could also be done before handing the files. 
                            foreach (string str in subDirs)
                            {
                                bool isDirectoryRestricted = CheckIfDirectoryIsRestricted(str);
                                if (isDirectoryRestricted)
                                {
                                    this._excludedFiles.Add(str, Enumerations.FileCategory.RestrictedDirectory);
                                    continue;
                                }

                                var attributes = File.GetAttributes(str);

                                if (!Utilities.IsJunction(str))
                                {
                                    dirs.Push(str);
                                }
                            }

                            if (currentDir != root)
                                allSubDirs.Add(currentDir);
                        }
                    });

                return allSubDirs;
            }
            catch(Exception e)
            { throw e; }
        }

        private void SetTotalsTexts()
        {
            ArchiveSizeText = Properties.Resources.TotalArchiveSizeText +
                            LongToSizeString.ConvertToString((double)this._archiveSize) + ", ";

            this.TotalFilesToArchiveText = this._archiveInfo.Count + 
                (this._archiveInfo.Count == 1 ? " file. " : " files. ");

            if (_excludedFiles.Count > 0)
            {
                this.TotalFilesToArchiveText = this.TotalFilesToArchiveText.Replace('.', ',');
                this.TotalFilesToExcludeText = this._excludedFiles.Count +
                    (this._excludedFiles.Count == 1 ? " file" : " files") + 
                    " will not be uploaded.";
            }
        }

        private bool CheckIfDirectoryIsRestricted(string path)
        {
            // if the directory is the %USERPROFILE%\AppData or the %windir% directory
            // then exclude it from the file and subdir search.
            // show a warning dialog that it's going to be excluded and that if the user
            // wants to back it up, she should use a backup utility and then choose
            // to upload that backup file.
            string localAppData = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            IList<string> restrictedDirs = new List<string>()
                                            {
                                                localAppData.ToLower(),
                                                windowsDir.ToLower()
                                            };

            if (restrictedDirs.Contains(path.ToLower()))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if the selection includes restricted folders and show a warning message.
        /// </summary>
        /// <returns></returns>
        private async Task<MessageBoxResult> ShowRestrictedWarningMessage()
        {
            string localAppData = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            IList<string> restrictedDirs = new List<string>()
                                            {
                                                localAppData,
                                                windowsDir
                                            };

            var restrictedDirsIntersection = this._excludedFiles.Keys.Intersect(restrictedDirs, StringComparer.InvariantCultureIgnoreCase);

            if (restrictedDirsIntersection.Count() > 0)
            {
                string title = Properties.Resources.WarningTitleText;

                StringBuilder messageSb = new StringBuilder();
                messageSb.Append(Properties.Resources.RestrictedSelectionWarningFirstPartText);
                if (restrictedDirsIntersection.Count() > 1)
                    messageSb.Append("s");

                messageSb.AppendLine(" " + Properties.Resources.RestrictedSelectionWarningSecondPartText);
                messageSb.AppendLine();

                foreach (string dir in restrictedDirsIntersection)
                {
                    messageSb.Append(" \"");
                    messageSb.Append(dir);
                    messageSb.Append("\" ");
                    messageSb.Append(" (contains ");

                    if (dir.ToLower() == localAppData.ToLower())
                        messageSb.Append(Properties.Resources.AppDataDirExplanationText);
                    else if (dir.ToLower() == windowsDir.ToLower())
                        messageSb.Append(Properties.Resources.WindowsDirExplanationText);

                    messageSb.AppendLine(")");
                }

                messageSb.AppendLine();
                messageSb.Append(Properties.Resources.RestrictedSelectionWarningThirdPartText);

                if (restrictedDirsIntersection.Count() == 1)
                {
                    messageSb.Append(" " + Properties.Resources.ThisFolderText);
                }
                else if (restrictedDirsIntersection.Count() > 1)
                {
                    messageSb.Append(" " + Properties.Resources.ThoseFoldersText);
                }

                messageSb.AppendLine(" " + Properties.Resources.ClickCancelToCancelText + ".");
                messageSb.AppendLine();
                messageSb.Append(Properties.Resources.RestrictedSelectionWarningFourthPartText);

                //if (restrictedDirsIntersection.Count() == 1)
                //{
                //    messageSb.Append(" " + Properties.Resources.ThisFolderText + " ");
                //}
                //else if (restrictedDirsIntersection.Count() > 1)
                //{
                //    messageSb.Append(" " + Properties.Resources.ThoseFoldersText + " ");
                //}

                //messageSb.Append(Properties.Resources.RestrictedSelectionWarningFifthPartText);

                MessageBoxButton button = MessageBoxButton.OKCancel;

                var windowManager = IoC.Get<IWindowManager>();
                var result = await windowManager.ShowMessageViewModelAsync(messageSb.ToString(), title, button);

                return result;
            }
            else
            {
                return MessageBoxResult.None;
            }
        }

        #endregion

        #region message_handlers

        public void Handle(IInternetConnectivityMessage message)
        {
            if (message != null)
            {
                NotifyOfPropertyChange(() => this.IsInternetConnected);
                NotifyOfPropertyChange(() => this.UploadButtonTooltipText);
                NotifyOfPropertyChange(() => this.CanUpload);
            }
        }

        #endregion

        #region events

        protected override void OnActivate()
        {
            this._eventAggregator.Subscribe(this);

            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            this.Reset();
            this._eventAggregator.Unsubscribe(this);

            base.OnDeactivate(close);
        }

        #endregion
    }
}
