// Ported 1:1 from Winhance (Winhance.Core/Features/SoftwareApps/Models).
// Data catalog — keep in sync with upstream when updating.

namespace AkariTool.Tabs;

public static partial class ExternalAppCatalog
{
    public static class DocumentViewers
    {
        public static AppGroup GetDocumentViewers()
        {
            return new AppGroup
            {
                Name = "Document Viewers",
                FeatureId = "ExternalApps",
                Items = new List<AppDefinition>
                {
                    new AppDefinition
                    {
                        Id = "external-app-libreoffice",
                        Name = "LibreOffice",
                        Description = "Free and open-source office suite",
                        RegistryDisplayName = "LibreOffice {version}",
                        GroupName = "Document Viewers",
                        AppxPackageName = ["TheDocumentFoundation.LibreOfficeMSIX"],
                        WinGetPackageId = ["TheDocumentFoundation.LibreOffice"],
                        ChocoPackageId = "libreoffice-fresh",
                        MsStoreId = "9PB80DCFP83W",
                        WebsiteUrl = "https://www.libreoffice.org/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-onlyoffice",
                        Name = "ONLYOFFICE Desktop Editors",
                        Description = "100% open-source free alternative to Microsoft Office",
                        RegistryDisplayName = "ONLYOFFICE {version} ({arch})",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["ONLYOFFICE.DesktopEditors"],
                        ChocoPackageId = "onlyoffice",
                        MsStoreId = "XPDLH3XBZXQV23",
                        WebsiteUrl = "https://www.onlyoffice.com/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-pdfgear",
                        Name = "PDFgear",
                        Description = "Free PDF reader, editor, converter, and signer with no sign-up required",
                        GroupName = "Document Viewers",
                        RegistrySubKeyName = "{7DACF63A-4EE4-4837-9AF9-C65D4509FFB4}_is1",
                        RegistryDisplayName = "PDFgear {version}",
                        WinGetPackageId = ["PDFgear.PDFgear"],
                        ChocoPackageId = "pdfgear",
                        MsStoreId = "XPDLNJ2FWVCXR1",
                        WebsiteUrl = "https://www.pdfgear.com/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-foxit-reader",
                        Name = "Foxit PDF Reader",
                        Description = "Lightweight PDF reader with advanced features",
                        GroupName = "Document Viewers",
                        MsStoreId = "XPFCG5NRKXQPKT", // MS Store package
                        ChocoPackageId = "foxitreader",
                        WebsiteUrl = "https://www.foxit.com/pdf-reader/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-sumatra-pdf",
                        Name = "SumatraPDF",
                        Description = "PDF, eBook (epub, mobi), comic book (cbz/cbr), DjVu, XPS, CHM, image viewer for Windows",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["SumatraPDF.SumatraPDF"],
                        ChocoPackageId = "sumatrapdf",
                        WebsiteUrl = "https://www.sumatrapdfreader.org/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-openoffice",
                        Name = "OpenOffice",
                        Description = "Largely inactive open-source office suite; users typically migrate to LibreOffice",
                        RegistryDisplayName = "OpenOffice {version}",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["Apache.OpenOffice"],
                        ChocoPackageId = "openoffice",
                        MsStoreId = "XP89J5462CMGJD",
                        WebsiteUrl = "https://www.openoffice.org/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-adobe-reader",
                        Name = "Adobe Acrobat Reader DC",
                        Description = "Adobe's free PDF reader with form filling and basic markup",
                        RegistryDisplayName = "Adobe Acrobat ({arch})",
                        GroupName = "Document Viewers",
                        MsStoreId = "XPDP273C0XHQH2", // MS Store package
                        ChocoPackageId = "adobereader",
                        WebsiteUrl = "https://www.adobe.com/acrobat/pdf-reader.html",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-evernote",
                        Name = "Evernote",
                        Description = "Cloud-synced notebook for typed notes, web clips, and tasks",
                        RegistryDisplayName = "Evernote {version}",
                        GroupName = "Document Viewers",
                        AppxPackageName = ["Evernote.Evernote"],
                        WinGetPackageId = ["Evernote.Evernote"],
                        ChocoPackageId = "evernote",
                        MsStoreId = "9WZDNCRFJ3MB",
                        WebsiteUrl = "https://evernote.com/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-cherrytree",
                        Name = "CherryTree",
                        Description = "Hierarchical note taking application with rich text and syntax highlighting",
                        RegistryDisplayName = "CherryTree version {version}",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["Giuspen.Cherrytree"],
                        ChocoPackageId = "cherrytree",
                        WebsiteUrl = "https://www.giuspen.net/cherrytree/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-okular",
                        Name = "Okular",
                        Description = "Universal document viewer supporting PDF, eBook, and more",
                        GroupName = "Document Viewers",
                        AppxPackageName = ["KDEe.V.Okular"],
                        WinGetPackageId = ["KDE.Okular", "KDE.Okular.AppX"],
                        ChocoPackageId = "okular",
                        MsStoreId = "9N41MSQ1WNM8",
                        WebsiteUrl = "https://okular.kde.org/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-pdf24-creator",
                        Name = "PDF24 Creator",
                        Description = "Free PDF creator and converter",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["geeksoftwareGmbH.PDF24Creator"],
                        ChocoPackageId = "pdf24",
                        MsStoreId = "XPFD51H3VQZFM0",
                        WebsiteUrl = "https://www.pdf24.org/",
                    },
                    new AppDefinition
                    {
                        Id = "external-app-microsoft-365",
                        Name = "Microsoft 365",
                        Description = "Microsoft Office productivity suite",
                        RegistrySubKeyName = "O365ProPlusRetail - {locale}",
                        RegistryDisplayName = "Microsoft 365 Apps for enterprise - {locale}",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["Microsoft.Office"],
                        ChocoPackageId = "office365business",
                        MsStoreId = "9WZDNCRD29V9",
                        WebsiteUrl = "https://www.microsoft.com/microsoft-365",
                    }
                }
            };
        }
    }
}
