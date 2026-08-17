// Ported 1:1 from Winhance (Winhance.Core/Features/SoftwareApps/Models).
// Data catalog — keep in sync with upstream when updating.

namespace AkariTool.Tabs;

public static partial class ExternalAppCatalog
{
    public static class Compression
    {
        public static AppGroup GetCompression()
        {
            return new AppGroup
            {
                Name = "Compression",
                FeatureId = "ExternalApps",
                Items = new List<AppDefinition>
                {
                    new AppDefinition
                    {
                        Id = "external-app-7zip",
                        Name = "7-Zip",
                        Description = "Open-source file archiver with a high compression ratio",
                        GroupName = "Compression",
                        WinGetPackageId = ["7zip.7zip"],
                        ChocoPackageId = "7zip",
                        WebsiteUrl = "https://www.7-zip.org/",
                        // Wikimedia renders of 7-Zip's SVGs were too small/wordmark-
                        // heavy in a square cell. Embed the on-page icon mark instead.
                    },
                    new AppDefinition
                    {
                        Id = "external-app-winrar",
                        Name = "WinRAR archiver",
                        Description = "File archiver with a high compression ratio",
                        GroupName = "Compression",
                        WinGetPackageId = ["RARLab.WinRAR"],
                        ChocoPackageId = "winrar",
                        WebsiteUrl = "https://www.win-rar.com/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-peazip",
                        Name = "PeaZip",
                        Description = "Free file archiver utility. Open and extract RAR, TAR, ZIP files and more",
                        RegistryDisplayName = "PeaZip {version} ({arch})",
                        GroupName = "Compression",
                        WinGetPackageId = ["Giorgiotani.Peazip"],
                        ChocoPackageId = "peazip",
                        WebsiteUrl = "https://peazip.github.io/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-nanazip",
                        Name = "NanaZip",
                        Description = "Open source fork of 7-zip intended for the modern Windows experience",
                        GroupName = "Compression",
                        AppxPackageName = ["40174MouriNaruto.NanaZip"],
                        WinGetPackageId = ["M2Team.NanaZip"],
                        ChocoPackageId = "nanazip",
                        MsStoreId = "9N8G7TSCL18R",
                        WebsiteUrl = "https://github.com/M2Team/NanaZip",
                    }
                }
            };
        }
    }
}
