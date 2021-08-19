using System;
using System.Timers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;
using FirmwareInstaller.Services;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows;
using FirmwareInstaller.Framework.Mvvm;
using FirmwareInstaller.Services.Update;
using System.Runtime.CompilerServices;
using Octokit;
using System.IO;
using System.Text.RegularExpressions;

namespace FirmwareInstaller.ViewModels
{
    internal class MainViewModel : BaseViewModel
    {
        #region Constructor
        public MainViewModel()
        {
            _logLock = new object();

            _discoveryService = new DiscoveryService();
            _discoveryService.Error += OnServiceError;

            _downloadService = new DownloadService();
            _downloadService.Error += OnServiceError;

            _installService = new InstallService();
            _installService.Error += OnServiceError;

            IsBusy = true;
            InitLog();
            InitPorts();
            InitVersions();
            IsBusy = false;
        }
        #endregion

        #region Events
        #endregion

        #region Consts
        private readonly object _logLock;
        #endregion

        #region Fields
        private DiscoveryService _discoveryService;
        private DownloadService _downloadService;
        private InstallService _installService;

        private DelegateCommand _installCommand;
        private DelegateCommand<string> _loadFirmwareCommand;

        private COMDevice _selectedPort;
        private string _selectedVersion;
        private string _customFwFilePath;
        private IList<string> _logList;
        private string _log;
        private bool _isBusy = false;
        private bool _useOldBootloader = true;
        private bool _useCustomFw = false;
        #endregion

        #region Properties
        /// <summary>
        /// Indicates that sensitive operations are taking place and user 
        /// input should be blocked.
        /// </summary>
        public bool IsBusy 
        { 
            get => _isBusy;
            private set
            {
                _isBusy = value;
                RaisePropertyChanged();
                InstallCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Selected COM port.
        /// </summary>
        public COMDevice SelectedPort
        {
            get => _selectedPort;
            set => SetProperty(ref _selectedPort, value);
        }

        /// <summary>
        /// Selected version to download.
        /// </summary>
        public string SelectedVersion
        {
            get => _selectedVersion;
            set => SetProperty(ref _selectedVersion, value);
        }


        /// <summary>
        /// Selected version to download.
        /// </summary>
        public string CustomFwFilePath
        {
            get => _customFwFilePath;
            set
            {
                SetProperty(ref _customFwFilePath, value);
                InstallCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Indicates wether if the connected board is using
        /// the new or old bootloader. This is determines the baudrate
        /// used to upload the sketch.
        /// </summary>
        public bool UseOldBootloader
        {
            get => _useOldBootloader;
            set => SetProperty(ref _useOldBootloader, value);
        }

        /// <summary>
        /// Indicates wether if the connected board is using
        /// the new or old bootloader. This is determines the baudrate
        /// used to upload the sketch.
        /// </summary>
        public bool UseCustomFw
        {
            get => _useCustomFw;
            set
            {
                SetProperty(ref _useCustomFw, value);
                InstallCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// List of available COM ports.
        /// </summary>
        public ObservableCollection<COMDevice> Ports { get; private set; }

        /// <summary>
        /// List of available versions to download.
        /// </summary>
        public ObservableCollection<string> Versions { get; private set; }

        /// <summary>
        /// Plain text operations log.
        /// </summary>
        public string Log
        {
            get => _log;
            private set => SetProperty(ref _log, value);
        }

        #endregion

        #region Commands
        /// <summary>
        /// Starts the installation process using the selected COM
        /// port and version.
        /// </summary>
        public DelegateCommand InstallCommand
        {
            get
            {
                if (_installCommand == null)
                    _installCommand = new DelegateCommand(() => Install(), CanInstall);
                return _installCommand;
            }
        }

        /// <summary>
        /// Raises the exitRequested event to start the process to shutdown the application.
        /// </summary>
        public DelegateCommand<string> LoadFirmwareCommand
        {
            get
            {
                if (_loadFirmwareCommand == null)
                    _loadFirmwareCommand = new DelegateCommand<string>(o => LoadFirmware(o));
                return _loadFirmwareCommand;
            }
        }
        #endregion

        #region Public Methods
        public override void Start()
        {
        }

        public override void Stop()
        {
        }
        #endregion

        #region Private Methods
        private void InitPorts()
        {
            SendLog("Discovering COM devices...");
            var ports = _discoveryService.Discover();
            Ports = new ObservableCollection<COMDevice>(ports);

            SelectedPort = Ports.FirstOrDefault();
        }

        private async void InitVersions()
        {
            SendLog("Retrieving available versions...");
            Versions = new ObservableCollection<string>();

            var versions = await _downloadService.RetrieveVersions();
            foreach (var version in versions)
                Versions.Add(version);

            SelectedVersion = Versions.FirstOrDefault();
        }

        private void InitLog()
        {
            lock(_logLock)
                _logList = new List<string>();
        }

        private void ClearLog()
        {
            lock (_logLock)
                _logList.Clear();

            Log = string.Empty;
        }

        private void SendLog(string message)
        {
            lock (_logLock)
                _logList.Add(message);

            var logString = string.Empty;

            lock (_logLock)
                foreach (var item in _logList)
                    logString += $"{item}\n";

            Log = logString;
        }

        private bool CanInstall()
        {
            if(IsBusy)
            {
                return false;
            }
            else if(SelectedPort is null)
            {
                return false;
            }
            else if (UseCustomFw)
            {
                return File.Exists(CustomFwFilePath);
            }
            else
            {
                return !string.IsNullOrEmpty(SelectedVersion);
            }
        }

        private async void Install()
        {
            IsBusy = true;
            ClearLog();

            var fwFilePath = CustomFwFilePath;
            if (!UseCustomFw)
            {

                SendLog($"Downloading version {_selectedVersion}...");
                fwFilePath = await _downloadService.DownloadVersionAsync(_selectedVersion);
                if (string.IsNullOrEmpty(fwFilePath))
                {
                    IsBusy = false;
                    return;
                }
            }

            SendLog($"Installing file {fwFilePath} to {_selectedPort.Port}");
            await _installService.InstallAsync(fwFilePath, _selectedPort.Port, _useOldBootloader);

            IsBusy = false;
        }

        private void LoadFirmware(string filePath)
        {
            SendLog($"Custom firmware loaded {Path.GetFileName(filePath)}");
            CustomFwFilePath = filePath;
            UseCustomFw = true;
        }
        #endregion

        #region EventHandlers
        private void OnServiceError(object sender, string e)
        {
            SendLog(e);
        }
        #endregion

        #region Overrides
        protected override void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            base.RaisePropertyChanged(name);

            if (name == nameof(SelectedPort) || name == nameof(SelectedVersion) || name == nameof(IsBusy))
                InstallCommand.RaiseCanExecuteChanged();
        }
        #endregion
    }
}