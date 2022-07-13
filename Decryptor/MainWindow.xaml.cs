using Ionic.Zip;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
//Todo: (performance)
// maybe Comment of true, should point to a zip inside zip, so we could really use password, in orther to defeat exe binary cheking.

namespace Decryptor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int MAIN_DECODER_SIZE = 841728; //new
        private string _executableLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        //private string _executableLocation = "test.exe";
        private string _tmpZipFileadress;
        private string _destinationFolder;
        private List<ZipItem> _zipItems;
        private bool _isExtracting = false;
        ZipFile anotherThreadZipFile; //used Beacuse if Thread on which extraction is taking place wa canceled , we could release the resource
        System.Threading.CancellationTokenSource cancellationtoken;
        private IList _selectedItem;
        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoad;
        }

        private void OnLoad(object sender, RoutedEventArgs e)
        {
            try
            {
                File.Copy(_executableLocation, "hsd2323xccxvrf423t444444442edsdx.tmp");

                using (var file = File.OpenRead("hsd2323xccxvrf423t444444442edsdx.tmp"))//create a tmp file and check if valid zip
                {
                    file.Position = MAIN_DECODER_SIZE;
                    using (FileStream tmpZipFile = File.OpenWrite(DateTime.Now.Ticks + ".tmp"))
                    {
                        _tmpZipFileadress = tmpZipFile.Name;
                        File.SetAttributes(tmpZipFile.Name,
                            FileAttributes.Hidden | FileAttributes.Temporary | FileAttributes.System);
                        file.CopyTo(tmpZipFile);
                    }
                }
                File.Delete("hsd2323xccxvrf423t444444442edsdx.tmp");
            }
            catch (System.UnauthorizedAccessException w)
            {
                //MessageBox.Show("خطای دسترسی به هارد.\nیکی از دلایل عمده ی این خطا عدم دسترسی به درایو میباشد\nلطفا با آدمین اجرا کرده یا محل فایل را عوض کنید\n" + w,
                //    "خطای دسترسی", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign);

                MessageBox.Show("Error in reading Disk!" + w,
                    "Accessibility Erorr!", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign);
                this.Close();
                Application.Current.Shutdown();
            }
            bool isValidZip;
            isValidZip = ZipFile.IsZipFile(_tmpZipFileadress, false);

            if (isValidZip == false)
            {
                //MessageBox.Show("فایل صحیح نمیباشد.",
                //        "خطای فایل", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign);
                MessageBox.Show("The File is not correct!",
                        "File Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign);
                Application.Current.Shutdown();
            }
            else
            {
                using (var zipFile = ZipFile.Read(_tmpZipFileadress))
                {
                    if (zipFile.Comment == "True")
                        ShowList();
                    else
                        GetPasswordFirst();
                }
            }
        }

        #region WindowEvent
        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (cancellationtoken != null) cancellationtoken.Cancel();
            if (anotherThreadZipFile != null) anotherThreadZipFile.Dispose();

            if (File.Exists(_tmpZipFileadress)) File.Delete(_tmpZipFileadress);
        }
        private void ClosePressed(object sender, RoutedEventArgs e)
        {
            bool shouldexist = true;
            if (_isExtracting == true)
            {
            //    var result = MessageBox.Show("برنامه در حال بازگشایی است، آیا از خروج اطمینان دارید؟",
            //"مشغول بودن برنامه", MessageBoxButton.OKCancel,
            //MessageBoxImage.Warning, MessageBoxResult.Cancel, MessageBoxOptions.RtlReading);

                var result = MessageBox.Show("Work is in Progress, are you sure?!",
        "Program is Busy!", MessageBoxButton.OKCancel,
        MessageBoxImage.Warning, MessageBoxResult.Cancel, MessageBoxOptions.RtlReading);

                shouldexist = (result == MessageBoxResult.OK) ? true : false;
            }
            if (shouldexist)
            {
                this.Close();
                App.Current.Shutdown();
            }
        }
        private void MinimizePressed(object sender, RoutedEventArgs e)
        {
            App.Current.MainWindow.WindowState = WindowState.Minimized;
        }

        private void ResizePressed(object sender, MouseButtonEventArgs e)
        {
            Application.Current.MainWindow.DragMove();
        }
        #endregion

        #region MainGridEvent
        private void ShowButtonVisibiliy(object sender, DependencyPropertyChangedEventArgs e)
        {

            if (ShowZipFileButton.Visibility == Visibility.Hidden)
            {
                this.Height = 500;
                this.ResizeMode = ResizeMode.CanResizeWithGrip;
            }
            else {
                this.Height = 100;
                this.ResizeMode = ResizeMode.CanMinimize;
            }

        }

        private void SelectAllClicked(object sender, RoutedEventArgs e)
        {
            if (SelectAllCheckBox.IsChecked == true)
                dataGrid.SelectAll();
            else
                dataGrid.UnselectAll();
        }

        private void PathSearchClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog()
            {
                Description = "Choose Folder to Save... ",
                ShowNewFolderButton = true,
            };
            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                AdressTextBox.Text = _destinationFolder = dialog.SelectedPath;
                StatusBlock.Text = "Please Continiue!";
            }
        }
        private void AdressTextboxLostFocus(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(AdressTextBox.Text) == false)
            {
                StatusBlock.Text = "Path is incorrect!";
                return;
            }
            _destinationFolder = AdressTextBox.Text;
            StatusBlock.Text = "Please Continiue.";
        }
        private void ShowList()
        {
            ShowZipFileButton.Visibility = Visibility.Hidden;
            PopulateDataGrid();


        }
        private void GetPasswordFirst()
        {
            ShowZipFileButton.Visibility = Visibility.Visible;
            //when showbutton is clikek and password is corrct , it'd showlist and etc
        }
        private void PopulateDataGrid()
        {
            _zipItems = new List<ZipItem>();
            using (var zipFile = ZipFile.Read(_tmpZipFileadress))
            {
                foreach (var ZipEntry in zipFile)
                {
                    SetZipOption(zipFile);
                    if (ZipEntry.FileName != "meLikePass")
                    {
                        _zipItems.Add(new ZipItem
                        {
                            FileName = ZipEntry.FileName,
                            LastModifiedTime = ZipEntry.LastModified,
                            Encrypted = ZipEntry.UsesEncryption,
                        });
                    }
                }
            }

            dataGrid.ItemsSource = _zipItems;
            dataGrid.SelectedItems.Clear();
            var style = new Style(typeof(DataGrid));
            foreach (var item in dataGrid.Columns)
            {
                if (item.Header.ToString() == "FileName")
                    item.Header = "File Name";
                if (item.Header.ToString() == "LastModifiedTime")
                    item.Header = "Last Change";
                if (item.Header.ToString() == "Encrypted")
                    item.Header = "Encrypted?";

            }
        }
        private bool CheckPassword(PasswordBox pass)
        {
            try
            {
                using (ZipFile zip = ZipFile.Read(_tmpZipFileadress))
                {
                    using (var memStream = new MemoryStream())
                    {
                        zip["meLikePass"].ExtractWithPassword(memStream, pass.Password);
                        return true;
                    }
                }
            }
            catch (BadPasswordException)
            {
                return false;
            }
        }
        private void ShowListClicked(object sender, RoutedEventArgs e)
        {
            if (CheckPassword(PasswordBox))
                ShowList();
            else
            {
                MessageBox.Show("Password is Wrong!", "Error!");
            }
        }
        private void SaveButtonLayoutUpdated(object sender, EventArgs e)
        {
            if (dataGrid.SelectedItems.Count > 0 &&
                Directory.Exists(AdressTextBox.Text) &&
                PasswordBox.Password.Length != 0)
            {
                SaveButton.IsEnabled = true;
            }
            else
            {
                SaveButton.IsEnabled = false;
            }
        }

        private void dataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedItem = dataGrid.SelectedItems;
        }

        #endregion
        private void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (CheckPassword(PasswordBox) == false)
            { MessageBox.Show("Password is Wrong.", "Error!"); return; }

            cancellationtoken = new System.Threading.CancellationTokenSource();
            MainGrid.IsEnabled = false;
            _isExtracting = true;

            var mainTask = Task.Factory.StartNew(Extract, new ExtractionArgs
            {
                pasbox = PasswordBox,
                adress = _destinationFolder,
                TmpZipFileAdress = _tmpZipFileadress
            }
            , cancellationtoken.Token,
                TaskCreationOptions.None,
                TaskScheduler.Default);
            mainTask.ContinueWith((x) =>
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MainGrid.IsEnabled = true;
                    _isExtracting = false;

                    StatusBlock.Text = "Extraction was Successfull. Please close program with the X buttom.";
                    ProgressBar.Value = 0;
                }));
            });
        }
        private void Extract(object Extractionargs)
        {
            try
            {
                var args = (ExtractionArgs)Extractionargs;

                using (anotherThreadZipFile = ZipFile.Read(args.TmpZipFileAdress))
                {
                    SetZipOption(anotherThreadZipFile);
                    anotherThreadZipFile.Password = args.pasbox.Password;
                    foreach (ZipItem item in _selectedItem)
                    {
                        var array = anotherThreadZipFile.Where(x => x.FileName == item.FileName).ToArray();
                        foreach (var zipEntry in array)
                        {
                            zipEntry.Extract(args.adress, ExtractExistingFileAction.OverwriteSilently);
                        }
                    }
                }
            }
            catch (Exception w)
            {
                MessageBox.Show(w.ToString());
            }
        }

        private void SetZipOption(ZipFile zip)
        {
            zip.ExtractProgress += zipFile_ExtractProgress;
            zip.AlternateEncodingUsage = ZipOption.Always;
            zip.AlternateEncoding = System.Text.Encoding.UTF8;
            zip.FlattenFoldersOnExtract = true;
        }

        private void zipFile_ExtractProgress(object sender, ExtractProgressEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ProgressBar.Maximum != e.TotalBytesToTransfer)
                    ProgressBar.Maximum = e.TotalBytesToTransfer;

                ProgressBar.Value = e.BytesTransferred;
            }));
        }
    }

    internal class ZipItem
    {
        public string FileName { get; set; }
        public DateTime LastModifiedTime { get; set; }
        public bool Encrypted { get; set; }
    }

    internal class ExtractionArgs
    {
        public PasswordBox pasbox { get; set; }
        public string adress { get; set; }

        public string TmpZipFileAdress;
    }
}
