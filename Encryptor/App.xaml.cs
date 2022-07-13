using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Encryptor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            //StartupUri = new Uri("pack://application:,,,/Encryptor;component/View/MainWindow.xaml");

            //var C = Path.GetPathRoot(Environment.SystemDirectory);
            //var mainDriveSize = DriveInfo.GetDrives().Where(x => x.Name == C).ToList()[0].TotalFreeSpace / (Math.Pow(1024, 3));
            if (System.IO.File.Exists(@"Decryptor\Decryptor.exe") != true)
            { MessageBox.Show("فایل رمز گشا وجود ندارد، لطفا با برنامه نویس تماس بگیرید"); App.Current.Shutdown(-1); }

            MahApps.Metro.ThemeManager.ChangeAppStyle
                (this
                , new MahApps.Metro.Accent("Cyan", new Uri("pack://application:,,,/MahApps.Metro;component/Styles/Accents/Cyan.xaml"))
                , new MahApps.Metro.AppTheme("BaseDark", new Uri("pack://application:,,,/MahApps.Metro;component/Styles/Accents/BaseDark.xaml")));

            base.OnStartup(e);
            ConfigureLogger();
        }


        /// <summary>
        /// log can be enabled with app.config file, but for portability,it is done by code
        /// </summary>
        public NLog.Config.LoggingConfiguration config;
        private void ConfigureLogger()
        {
            // Step 1. Create configuration object 
            config = new NLog.Config.LoggingConfiguration();

            // Step 2. Create targets and add them to the configuration 
            var fileTarget = new NLog.Targets.FileTarget();
            fileTarget.Name = "EncyoptorFileLogger";
            config.AddTarget("file", fileTarget);

            // Step 3. Set target properties 
            fileTarget.FileName = "${specialfolder:dir=HessamEncryptor:file=Encrytptor.log:folder=CommonApplicationData";
            //fileTarget.FileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            //    "HessamEncryptor", "Encryptor.log");
            fileTarget.Layout = "${time}|${shortdate}|${windows-identity}|${processtime}|${message}";

            // Step 4. Define rules
            var rule2 = new NLog.Config.LoggingRule("*", LogLevel.Debug, fileTarget);

            config.LoggingRules.Add(rule2);

            // Step 5. Activate the configuration
            LogManager.Configuration = config;
        }
    }
}
