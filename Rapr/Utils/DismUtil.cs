﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Dism;

namespace Rapr.Utils
{
    public class DismUtil : IDriverStore
    {
        public DriverStoreType Type { get; }

        public string OfflineStoreLocation { get; }

        public DismUtil()
        {
            this.Type = DriverStoreType.Online;
        }

        public DismUtil(string imagePath)
        {
            this.Type = DriverStoreType.Offline;
            this.OfflineStoreLocation = imagePath;
        }

        #region IDriverStore Members
        public List<DriverStoreEntry> EnumeratePackages()
        {
            List<DriverStoreEntry> driverStoreEntries = new List<DriverStoreEntry>();

            DismApi.Initialize(DismLogLevel.LogErrors);

            try
            {
                using (DismSession session = this.GetSession())
                {
                    List<DeviceDriverInfo> driverInfo = this.Type == DriverStoreType.Online
                        ? ConfigManager.GetDeviceDriverInfo()
                        : null;

                    foreach (var driverPackage in DismApi.GetDrivers(session, false))
                    {
                        DriverStoreEntry driverStoreEntry = new DriverStoreEntry
                        {
                            DriverClass = driverPackage.ClassDescription,
                            DriverInfName = Path.GetFileName(driverPackage.OriginalFileName),
                            DriverPublishedName = driverPackage.PublishedName,
                            DriverPkgProvider = driverPackage.ProviderName,
                            DriverSignerName = driverPackage.DriverSignature == DismDriverSignature.Signed ? SetupAPI.GetDriverSignerInfo(driverPackage.OriginalFileName) : string.Empty,
                            DriverDate = driverPackage.Date,
                            DriverVersion = driverPackage.Version,
                            DriverFolderLocation = Path.GetDirectoryName(driverPackage.OriginalFileName),
                            DriverSize = DriverStoreRepository.GetFolderSize(new DirectoryInfo(Path.GetDirectoryName(driverPackage.OriginalFileName))),
                            BootCritical = driverPackage.BootCritical,
                            Inbox = driverPackage.InBox,
                        };

                        var deviceInfo = driverInfo?.OrderByDescending(d => d.IsPresent)?.FirstOrDefault(e => string.Equals(
                            Path.GetFileName(e.DriverInf),
                            driverStoreEntry.DriverPublishedName,
                            StringComparison.OrdinalIgnoreCase));

                        driverStoreEntry.DeviceName = deviceInfo?.DeviceName;
                        driverStoreEntry.DevicePresent = deviceInfo?.IsPresent;

                        driverStoreEntries.Add(driverStoreEntry);
                    }
                }
            }
            finally
            {
                DismApi.Shutdown();
            }

            return driverStoreEntries;
        }

        private DismSession GetSession()
        {
            switch (this.Type)
            {
                case DriverStoreType.Online:
                    return DismApi.OpenOnlineSession();

                case DriverStoreType.Offline:
                    return DismApi.OpenOfflineSession(this.OfflineStoreLocation);

                default:
                    throw new NotSupportedException();
            }
        }

        public bool DeleteDriver(DriverStoreEntry driverStoreEntry, bool forceDelete)
        {
            if (driverStoreEntry == null)
            {
                throw new ArgumentNullException(nameof(driverStoreEntry));
            }

            switch (this.Type)
            {
                case DriverStoreType.Online:
                    return SetupAPI.DeleteDriver(driverStoreEntry, forceDelete);

                case DriverStoreType.Offline:
                    DismApi.Initialize(DismLogLevel.LogErrors);

                    try
                    {
                        using (DismSession session = this.GetSession())
                        {
                            DismApi.RemoveDriver(session, driverStoreEntry.DriverPublishedName);
                        }
                    }
                    finally
                    {
                        DismApi.Shutdown();
                    }

                    return true;

                default:
                    throw new NotSupportedException();
            }
        }

        public bool AddDriver(string infFullPath, bool install)
        {
            switch (this.Type)
            {
                case DriverStoreType.Online:
                    return SetupAPI.AddDriver(infFullPath, install);

                case DriverStoreType.Offline:
                    DismApi.Initialize(DismLogLevel.LogErrors);

                    try
                    {
                        using (DismSession session = this.GetSession())
                        {
                            DismApi.AddDriver(session, infFullPath, false);
                        }
                    }
                    finally
                    {
                        DismApi.Shutdown();
                    }

                    return true;

                default:
                    throw new NotSupportedException();
            }
        }

        #endregion
    }
}
