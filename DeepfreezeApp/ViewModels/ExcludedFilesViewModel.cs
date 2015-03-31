using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using Caliburn.Micro;
using System.IO;
using System.Diagnostics;
using MahApps.Metro.Controls.Dialogs;
using log4net;

namespace BigStash.WPF
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(IExcludedFilesViewModel))]
    public class ExcludedFilesViewModel : Screen, IExcludedFilesViewModel
    {
        #region fields

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(ExcludedFilesViewModel));

        private string _title;
        private string _excludedFilesText;
        private string _topMessageText;
        private string _bottomMessageText;
        private string _archiveTitle;

        #endregion

        #region properties

        public string Title
        {
            get { return this._title; }
            set { this._title = value; NotifyOfPropertyChange(() => this.Title); }
        }

        public string ExcludedFilesText
        {
            get { return this._excludedFilesText; }
            set { this._excludedFilesText = value; NotifyOfPropertyChange(() => this.ExcludedFilesText); }
        }

        public string TopMessageText
        {
            get { return this._topMessageText; }
            set { this._topMessageText = value; NotifyOfPropertyChange(() => this.TopMessageText); }
        }

        public string BottomMessageText
        {
            get { return this._bottomMessageText; }
            set { this._bottomMessageText = value; NotifyOfPropertyChange(() => this.BottomMessageText); }
        }

        public string ArchiveTitle
        {
            get { return this._archiveTitle; }
            set { this._archiveTitle = value; NotifyOfPropertyChange(() => this.ArchiveTitle); }
        }

        public double BodyWidth
        {
            get
            {
                var shellWindow = IoC.Get<IShell>().ShellWindow;
                return shellWindow.ActualWidth - 100;
            }
        }

        public string WhyText
        { get { return Properties.Resources.WhyFilesWereExcludedShortText; } }

        #endregion

        #region constructor

        #endregion

        #region action_methods

        public void Save()
        {
            // Show the FolderBrowserDialog.
            var saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.FileName = this.ArchiveTitle + " - Excluded Files";
            saveDialog.DefaultExt = ".txt";
            saveDialog.Filter = "Normal text file |.*txt";

            Nullable<bool> saveResult = saveDialog.ShowDialog();
            if (saveResult == true)
            {
                var savePath = saveDialog.FileName;

                StringBuilder finalText = new StringBuilder();
                finalText.AppendLine(Properties.Resources.ExcludedFilesTextFileParagraph);
                finalText.AppendLine(Properties.Settings.Default.BigStashNameRulesFAQURL);
                finalText.AppendLine();
                finalText.AppendLine();
                finalText.Append(this.ExcludedFilesText);

                try
                {
                    File.WriteAllText(savePath, finalText.ToString());
                    Process.Start(savePath);
                    TryClose();
                }
                catch (Exception e)
                {
                    _log.Error(Utilities.GetCallerName() + " threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");

                    var shellWindow = IoC.Get<IShell>().ShellWindow;
                    shellWindow.ShowMessageAsync("Error", "There was an error while saving the file: '" + savePath + "'.");
                }
            }
        }

        public void Close()
        {
            TryClose(false);
        }

        public void OpenNameRulesFAQPage()
        {
            Process.Start(Properties.Settings.Default.BigStashNameRulesFAQURL);
        }

        #endregion

        #region events

        #endregion
    }
}
