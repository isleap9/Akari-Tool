// Ported 1:1 from Winhance (Winhance.Core/Features/SoftwareApps/Models).
// Data catalog — keep in sync with upstream when updating.

namespace AkariTool.Tabs;

public static partial class ExternalAppCatalog
{
    public static class OnlineStorageBackup
    {
        public static AppGroup GetOnlineStorageBackup()
        {
            return new AppGroup
            {
                Name = "Online Storage & Backup",
                FeatureId = "ExternalApps",
                Items = new List<AppDefinition>
                {
                    new AppDefinition
                    {
                        Id = "external-app-google-drive",
                        Name = "Google Drive",
                        Description = "Cloud storage and file synchronization service",
                        GroupName = "Online Storage & Backup",
                        WinGetPackageId = ["Google.GoogleDrive"],
                        ChocoPackageId = "googledrive",
                        WebsiteUrl = "https://www.google.com/drive/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-dropbox",
                        Name = "Dropbox",
                        Description = "File hosting service that offers cloud storage, file synchronization, personal cloud",
                        GroupName = "Online Storage & Backup",
                        AppxPackageName = ["DropboxInc.Dropbox"],
                        WinGetPackageId = ["Dropbox.Dropbox"],
                        ChocoPackageId = "dropbox",
                        MsStoreId = "9NK4T08DHQ80",
                        WebsiteUrl = "https://www.dropbox.com/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-sugarsync",
                        Name = "SugarSync",
                        Description = "Automatically access and share your photos, videos, and files in any folder",
                        GroupName = "Online Storage & Backup",
                        WinGetPackageId = ["IPVanish.SugarSync"],
                        ChocoPackageId = "sugarsync",
                        WebsiteUrl = "https://www.sugarsync.com/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-nextcloud",
                        Name = "Nextcloud",
                        Description = "Self-hosted cloud platform for files, calendar, contacts, and chat",
                        GroupName = "Online Storage & Backup",
                        WinGetPackageId = ["Nextcloud.NextcloudDesktop"],
                        ChocoPackageId = "nextcloud-client",
                        WebsiteUrl = "https://nextcloud.com/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-proton-drive",
                        Name = "Proton Drive",
                        Description = "Secure cloud storage with end-to-end encryption",
                        GroupName = "Online Storage & Backup",
                        WinGetPackageId = ["Proton.ProtonDrive"],
                        ChocoPackageId = "protondrive",
                        WebsiteUrl = "https://proton.me/drive",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-freefilesync",
                        Name = "FreeFileSync",
                        Description = "Open-source folder comparison and synchronization tool",
                        GroupName = "Online Storage & Backup",
                        ChocoPackageId = "freefilesync",
                        WebsiteUrl = "https://freefilesync.org/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-hekasoft-backup",
                        Name = "Hekasoft Backup & Restore",
                        Description = "Backs up and restores browser bookmarks, settings, and profiles",
                        RegistryDisplayName = "Hekasoft Backup & Restore {version}",
                        GroupName = "Online Storage & Backup",
                        WinGetPackageId = ["Hekasoft.Backup-Restore"],
                        MsStoreId = "9NLJQ1B18MZT",
                        WebsiteUrl = "https://hekasoft.com/hekasoft-backup-restore/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://hekasoft.com/?download=112",
                            FallbackDownloadUrl = "https://hekasoft.com/?download=612",
                        },
                        // Icon resolved via MS Store CDN (Layer 2a). No trusted catalog URL.
                    },
                }
            };
        }
    }
}
