using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Ionic.Zlib;
using Ionic.Zip;
using MahApps.Metro.Controls.Dialogs;
using System.Threading;
using Encryptor.Helper;
using GalaSoft.MvvmLight.Command;

namespace Encryptor.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase, System.ComponentModel.IDataErrorInfo
    {
        #region Fields

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private string _default_txt = "Destination: ";
        private string _saveto;
        private bool _inMemory;
        private string _status;
        private bool _progressValue;
        private bool _isSourceDir = true;
        private bool _isSourceFound = false;
        private List<string> fileNames;
        private bool _showListFirst = true;

        private bool _quiet = true;
        private bool _isGridEnabled = true;
        private IDialogCoordinator _dialog;
        #endregion

        #region Property
        public GalaSoft.MvvmLight.Command.RelayCommand SearchDestinationCommand { get; private set; }
        public GalaSoft.MvvmLight.Command.RelayCommand<System.Windows.Controls.PasswordBox> StartCommand { get; private set; }
        public GalaSoft.MvvmLight.Command.RelayCommand SearchSourceCommand { get; private set; }
        public RelayCommand<System.ComponentModel.CancelEventArgs> OnClosingCommand { get; private set; }
        public IEnumerable<Ionic.Zlib.CompressionLevel> StreamBasedCompressions
        {
            get
            {
                return Enum.GetValues(typeof(Ionic.Zlib.CompressionLevel))
                    .Cast<Ionic.Zlib.CompressionLevel>().Distinct().ToList();

            }
        }
        public Enum SelectedCompressOption { get; set; }

        public string Default_TXT
        {
            get { return _default_txt; }
            set { Set(() => Default_TXT, ref _default_txt, value); }
        }
        public string SAVETO
        {
            get { return _saveto; }
            set
            {
                // if (Directory.Exists(value))
                //{
                Set(() => SAVETO, ref _saveto, value,true);
                //}
            }
        }
        public bool InMemory
        {
            get { return _inMemory; }
            set { Set(() => InMemory, ref _inMemory, value); }
        }
        public bool ProgressValue
        {
            get { return _progressValue; }
            set { Set(() => ProgressValue, ref _progressValue, value, true); }
        }
        public string STATUS
        {
            get { return _status; }
            set { Set(() => STATUS, ref _status, value, true); }
        }
        public bool IsSourceDir
        {
            get { return _isSourceDir; }
            set
            {
                Set(() => IsSourceDir, ref _isSourceDir, value);
                IsSourceFound = false;
                fileNames = null;
            }
        }
        public bool IsSourceFound
        {
            get { return _isSourceFound; }
            set { Set(() => IsSourceFound, ref _isSourceFound, value); }
        }
        public bool IsGridEnabled
        {
            get { return _isGridEnabled; }
            set { Set(() => IsGridEnabled, ref _isGridEnabled, value); }
        }
        public bool Quiet
        {
            get { return _quiet; }
            set { Set(() => Quiet, ref _quiet, value); }
        }
        int numberofretry = 0;
        public bool ShowListFirst
        {
            get { return _showListFirst; }
            set { Set(() => ShowListFirst, ref _showListFirst, value); }
        }

        #endregion


        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel(IDialogCoordinator dialog)
        {
            _dialog = dialog;
            _logger.Log(NLog.LogLevel.Info, "Main View Model Started!");

            _saveto = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _status = "Welcome! Fill in the blanks!! ";

            StartCommand = new RelayCommand<System.Windows.Controls.PasswordBox>(CreateZip, CanStartCreating);
            SearchDestinationCommand = new RelayCommand(GetAdress);
            SearchSourceCommand = new GalaSoft.MvvmLight.Command.RelayCommand(FindSource);
            OnClosingCommand = new RelayCommand<System.ComponentModel.CancelEventArgs>(OnClosingAction);

            GalaSoft.MvvmLight.Threading.DispatcherHelper.Initialize();

            _logger.Log(NLog.LogLevel.Info, "Default parameter has been Initialized. Default Path Set to {0}", _saveto);
        }

        private void OnClosingAction(System.ComponentModel.CancelEventArgs e)
        {
            _logger.Info("Close Requested!");
            _logger.Info("Progress Value : {0}",ProgressValue);
            if (ProgressValue)
            {
                e.Cancel = true;
                _dialog.ShowMessageAsync(this, "Closing App", "Program is Running, Are you sure you want to quit ?",
                MessageDialogStyle.AffirmativeAndNegative,
                new MetroDialogSettings { AffirmativeButtonText = "Yep!", NegativeButtonText = "Nope!", DialogTitleFontSize = 14, DialogMessageFontSize = 12 })
                .ContinueWith(exit(e));
            }
            else
            {
                e.Cancel = false;
                _logger.Info("App Set to Close!");
            }
        }

        private Action<Task<MessageDialogResult>> exit(System.ComponentModel.CancelEventArgs e)
        {
            return (x) =>
            {
                _logger.Info("Asked user of his certainty ! Answer : {0}",x.Result);
                switch (x.Result)
                {
                    case MessageDialogResult.Negative:
                        break;
                    case MessageDialogResult.Affirmative:
                        cancelToken.Cancel();
                        if (_inMemory != true)
                        {
                            try
                            {
                                _logger.Info("Closing and Deleting file stream");
                                var zipfile = ((FileStream)zipStream).Name;
                                zipStream.Close();
                                zipStream.Dispose();
                                File.Delete(zipfile);
                            }
                            catch (Exception w)
                            {
                                _logger.Warn(w, "Getting ZipStreamFileName throwed Exception");
                            }
                        }
                        GalaSoft.MvvmLight.Threading.DispatcherHelper.CheckBeginInvokeOnUI(() => App.Current.Shutdown());
                        _logger.Info("Shuding down app -> Should be Closed");
                        break;
                    default:
                        break;
                }
            };
        }

        private bool CanStartCreating(System.Windows.Controls.PasswordBox arg)
        {
            if (arg == null || Error != string.Empty) return false;


            if (IsSourceFound && arg.Password.Length > 0 && SelectedCompressOption != null)
            {
                if (arg.Password.Length < 4)
                {
                    STATUS = "Pass Should be longer than 4 charachter !!";
                    return false;
                }
                STATUS = "Click to START !!";
                return true;
            }
            return false;
        }

        private StringWriter writer; //logger writes out this writer used by zip
        private CancellationTokenSource cancelToken;//used if user wantet to exit program so i could delete file
        private Stream zipStream;
        private void CreateZip(System.Windows.Controls.PasswordBox obj)
        {
            _logger.Info("Create Zip Started!");
            ProgressValue = true;
            IsGridEnabled = false;
            STATUS = "PLease Wait !!";
            cancelToken = new CancellationTokenSource();
            var t = Task.Factory.StartNew(RunZipping, obj, cancelToken.Token,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
            t.ContinueWith(StartAppending);
        }
        private void RunZipping(object obj)
        {
            var pas = (System.Windows.Controls.PasswordBox)obj;
            if (_inMemory)
                zipStream = new MemoryStream();
            else
            {
                zipStream = new FileStream(SAVETO + "\\" + DateTime.Now.Ticks + ".tmp", FileMode.Create, FileAccess.ReadWrite, FileShare.Inheritable);
                File.SetAttributes(((FileStream)zipStream).Name, FileAttributes.Hidden | FileAttributes.Temporary | FileAttributes.System);
            }
            _logger.Info("Stream Created !");
            writer = new StringWriter();

            using (var zip = new Ionic.Zip.ZipFile())
            {
                zip.Comment = (ShowListFirst) ? "True" : "False"; //my option for exraction, Get password first or show content list

                #region Option
                zip.CompressionLevel = (CompressionLevel)SelectedCompressOption;
                zip.CompressionMethod = Ionic.Zip.CompressionMethod.Deflate;

                zip.AddDirectoryWillTraverseReparsePoints = false;

                zip.AlternateEncoding = System.Text.Encoding.UTF8;
                zip.AlternateEncodingUsage = Ionic.Zip.ZipOption.Always;
                
                zip.UseZip64WhenSaving = Ionic.Zip.Zip64Option.AsNecessary;
                zip.Strategy = CompressionStrategy.Default;
                zip.EmitTimesInWindowsFormatWhenSaving = true;

                zip.Encryption = Ionic.Zip.EncryptionAlgorithm.WinZipAes256;
                zip.Password = pas.Password;
                #endregion

                #region Events
                zip.ZipError += LogZipErrors; //handles error action
                zip.AddProgress += LogZipProgress;
                zip.ReadProgress += LogReadProgress;
                zip.SaveProgress += LogSaveProgress;
                #endregion
                zip.StatusMessageTextWriter = writer; //logger use this to write to log

                if (IsSourceDir)
                    zip.AddDirectory(fileNames[0]);
                else
                    zip.AddFiles(fileNames);

                zip.AddEntry("meLikePass", "This is used for PasswordCheck!!");

                try
                {
                    zip.Save(zipStream);
                }
                catch (Exception w)
                {
                    if (cancelToken.IsCancellationRequested != true)
                    {
                        MessageBox.Show("See the Mighty Log!!");
                        _logger.Error(w, "Saving Error");
                    }
                }
            }

            _logger.Info(writer);
        }
        private void StartAppending(Task obj)
        {
            if (obj.IsCompleted && cancelToken.IsCancellationRequested != true)
            {
                _logger.Info("Zip Created , Appending ...");
                using (var dycryptor = new FileStream(@"Decryptor\Decryptor.exe", FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (var file = new FileStream(SAVETO + "\\" + DateTime.Now.Ticks + ".exe", FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        dycryptor.CopyTo(file);
                        zipStream.Seek(0, SeekOrigin.Begin);
                        zipStream.CopyTo(file);
                    }
                }
                _logger.Info("deleting tmp file and closing stream ...");
                if (_inMemory != true)
                {
                    string tmpAdress = ((FileStream)zipStream).Name;
                    zipStream.Close();
                    zipStream.Dispose();
                    File.Delete(tmpAdress);
                }
                zipStream.Close();
                zipStream.Dispose();

                _logger.Info("Done! Showing Confirmation.");
                GalaSoft.MvvmLight.Threading.DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    _dialog.ShowMessageAsync(this, "Success", "Creating file Completed!!",
                    MessageDialogStyle.Affirmative,
                    new MetroDialogSettings { AffirmativeButtonText = "Okay!", DialogTitleFontSize = 14, DialogMessageFontSize = 12 });

                    ProgressValue = false;
                    IsGridEnabled = true;
                });
            }
        }

        private void LogSaveProgress(object sender, SaveProgressEventArgs e)
        {
            if (e.EventType == ZipProgressEventType.Saving_Started && e.EventType == ZipProgressEventType.Saving_Completed)
            {
                _logger.Info("{0} for {1} .", e.EventType, e.ArchiveName);
            }
            if (e.EventType == ZipProgressEventType.Error_Saving)
            {
                _logger.Error("{0} for {1} .", e.EventType, e.ArchiveName);
            }
        }

        private void LogReadProgress(object sender, ReadProgressEventArgs e)
        {//reading
            if (e.EventType == ZipProgressEventType.Reading_Started && e.EventType == ZipProgressEventType.Reading_Completed)
            {
                _logger.Info("{0} for {1} .", e.EventType, e.ArchiveName);
            }
        }

        private void LogZipProgress(object sender, AddProgressEventArgs e)
        {//adding
            if (e.EventType == ZipProgressEventType.Adding_Started && e.EventType == ZipProgressEventType.Adding_Completed)
            {
                _logger.Info(e.EventType);
            }
        }

        private void LogZipErrors(object sender, ZipErrorEventArgs e)
        {
            _logger.Error(e.Exception);

            if (Quiet)
            {
                while (numberofretry < 10)
                {
                    e.CurrentEntry.ZipErrorAction = ZipErrorAction.Retry;
                    numberofretry++;
                }
                e.CurrentEntry.ZipErrorAction = ZipErrorAction.Skip;
            }
            else
            {
                while (numberofretry < 2)
                {
                    MessageBox.Show("Solve the Problen and try later!\n" + e.Exception, "Error!", MessageBoxButton.OK, MessageBoxImage.Exclamation, MessageBoxResult.OK, MessageBoxOptions.RightAlign);
                    e.CurrentEntry.ZipErrorAction = ZipErrorAction.Retry;
                    numberofretry++;
                }
                e.CurrentEntry.ZipErrorAction = ZipErrorAction.Skip;
            }
        }

        private void FindSource()
        {
            if (IsSourceDir)
            {
                var tmp = DirectoryBrowsing();
                if (tmp != null)
                {
                    fileNames = new List<string>();
                    fileNames.Add(tmp);
                }
            }
            else
            {
                fileNames = FileBrowsing().ToList();
            }
            IsSourceFound = (fileNames == null) ? false : true;
            _logger.Info("Finding source, Is Source Found : {0}",IsSourceFound);
            _logger.Info("Source : {0}", fileNames);
        }
        private void GetAdress()
        {
            SAVETO = DirectoryBrowsing() ?? SAVETO;
            _logger.Info("Path was Change to: {0}", SAVETO);
        }


        /// <summary>
        /// Ask user to brwose and choose a directory
        /// returns null if user cancels the operation
        /// </summary>
        private string DirectoryBrowsing()
        {
            try
            {
                var s = new WPFFolderBrowser.WPFFolderBrowserDialog();
                s.Title = "Choose desired folder !";
                if (s.ShowDialog() == true)
                {
                    return s.FileName;
                }
            }
            catch (Exception w)
            {
                _logger.Error(w, "Error at Directory Browsing ");
                _status = "There is an Error ! Send The mighty Log to the Police!";
            }
            return null;
        }
        private string[] FileBrowsing()
        {
            var fd = new OpenFileDialog()
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = true,
                DefaultExt = "*.*",
                Filter = "All files (*.*)|*.*;",
            };

            if (fd.ShowDialog() == true)
            {
                return fd.FileNames;
            }
            return null;
        }

        #region DataErrorInfo
        private string _error;
        public string Error
        {
            get
            {
                return _error;
            }
            private set
            {
                Set(() => Error, ref _error, value);
            }
        }

        public string this[string columnName]
        {
            get
            {
                var info = this.GetType().GetProperty(columnName);
                if (info.Name == "SAVETO")
                {
                    if (info != null)
                    {
                        var data = info.GetValue(this, null) as string;
                        if (Directory.Exists(data) != true)
                        {
                            STATUS = Error = "Invalid Path!";
                            return Error;
                        }
                        Error = string.Empty;
                    }
                }
                return null;
            }
        }
        #endregion

    }
}