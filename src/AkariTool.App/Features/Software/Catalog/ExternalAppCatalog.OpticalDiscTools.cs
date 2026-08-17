// Ported 1:1 from Winhance (Winhance.Core/Features/SoftwareApps/Models).
// Data catalog — keep in sync with upstream when updating.

namespace AkariTool.Tabs;

public static partial class ExternalAppCatalog
{
    public static class OpticalDiscTools
    {
        public static AppGroup GetOpticalDiscTools()
        {
            return new AppGroup
            {
                Name = "Optical Disc Tools",
                FeatureId = "ExternalApps",
                Items = new List<AppDefinition>
                {
                    new AppDefinition
                    {
                        Id = "external-app-imgburn",
                        Name = "ImgBurn",
                        Description = "Lightweight CD / DVD / HD DVD / Blu-ray burning application",
                        GroupName = "Optical Disc Tools",
                        WinGetPackageId = ["LIGHTNINGUK.ImgBurn"],
                        ChocoPackageId = "imgburn",
                        WebsiteUrl = "https://www.imgburn.com/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-anyburn",
                        Name = "AnyBurn",
                        Description = "Lightweight CD/DVD/Blu-ray burning software",
                        GroupName = "Optical Disc Tools",
                        WinGetPackageId = ["PowerSoftware.AnyBurn"],
                        WebsiteUrl = "http://www.anyburn.com/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://www.anyburn.com/anyburn_setup.exe",
                        },
                    },
                    new AppDefinition
                    {
                        Id = "external-app-cdburnerxp",
                        Name = "CDBurnerXP",
                        Description = "Free CD/DVD/Blu-ray burning software",
                        RegistryDisplayName = "CDBurnerXP",
                        GroupName = "Optical Disc Tools",
                        ChocoPackageId = "cdburnerxp",
                        WebsiteUrl = "https://cdburnerxp.se/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-makemkv",
                        Name = "MakeMKV",
                        Description = "DVD and Blu-ray to MKV converter and streaming tool",
                        RegistryDisplayName = "MakeMKV {version}",
                        GroupName = "Optical Disc Tools",
                        WinGetPackageId = ["GuinpinSoft.MakeMKV"],
                        ChocoPackageId = "makemkv",
                        WebsiteUrl = "https://www.makemkv.com/",
                    }
                }
            };
        }
    }
}
