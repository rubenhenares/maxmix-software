using System;
using System.Collections.Generic;
using System.Management;
using System.Text.RegularExpressions;

namespace FirmwareInstaller.Services
{
    /// <summary>
    /// Represents a serial device.
    /// </summary>
    internal class COMDevice: IComparable<COMDevice>
    {
        public string Port { get; set; }
        public string Name { get; set; }

        #region Constructor
        public COMDevice() { }
        #endregion

        #region Overrides

        public int CompareTo(COMDevice other)
        {
            int currentNumber = Int32.Parse(Port.Substring(Port.Length - 1));
            int otherNumber = Int32.Parse(other.Port.Substring(other.Port.Length - 1));

            return currentNumber.CompareTo(otherNumber);
        }
        #endregion
    }

    /// <summary>
    /// Provides functionality to find available COM ports.
    /// </summary>
    internal class DiscoveryService : BaseService
    {
        #region Fields
        private const string _usbDeviceQueryString = @"SELECT name FROM Win32_PnPEntity";
        private readonly Regex _comPortRegex = new Regex(@"COM\d", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        #endregion

        #region Constructor
        public DiscoveryService() { }
        #endregion

        #region Public Methods
        /// <summary>
        /// Retrieves a list of available devices on COM ports.
        /// </summary>
        /// <returns>List of available devices on COM ports.</returns>
        public IEnumerable<COMDevice> Discover()
        {
            ManagementObjectCollection usbDevices = queryUSBDevices();
            List<COMDevice> serialDevices = filterCOMDevice(usbDevices);
            serialDevices.Sort();
            return serialDevices;
        }
        #endregion

        #region Private Methods
        private ManagementObjectCollection queryUSBDevices()
        {
            var moSearch = new ManagementObjectSearcher(_usbDeviceQueryString);
            return moSearch.Get();
        }

        private List<COMDevice> filterCOMDevice(ManagementObjectCollection usbDevices)
        {
            var serialDevices = new List<COMDevice>();

            foreach (ManagementObject device in usbDevices)
            {
                object deviceName = device.Properties["Name"].Value;
                if (deviceName != null && _comPortRegex.IsMatch(deviceName.ToString()))
                {
                    serialDevices.Add(new COMDevice { Port = _comPortRegex.Match(deviceName.ToString()).Value, Name = deviceName.ToString()});
                }
            }
            return serialDevices;
        }
        #endregion
    }
}