// Ported 1:1 from Winhance (Winhance.Core/Features/SoftwareApps/Models).
// Data catalog — keep in sync with upstream when updating.

namespace AkariTool.Tabs;

public static partial class ExternalAppCatalog
{
    public static AppGroup GetExternalApps()
    {
        var allItems = new List<AppDefinition>();

        // Add all category items
        allItems.AddRange(Browsers.GetBrowsers().Items);
        allItems.AddRange(DocumentViewers.GetDocumentViewers().Items);
        allItems.AddRange(MessagingEmailCalendar.GetMessagingEmailCalendar().Items);
        allItems.AddRange(OnlineStorageBackup.GetOnlineStorageBackup().Items);
        allItems.AddRange(Multimedia.GetMultimedia().Items);
        allItems.AddRange(Imaging.GetImaging().Items);
        allItems.AddRange(CustomizationUtilities.GetCustomizationUtilities().Items);
        allItems.AddRange(Gaming.GetGaming().Items);
        allItems.AddRange(Compression.GetCompression().Items);
        allItems.AddRange(FileDiskManagement.GetFileDiskManagement().Items);
        allItems.AddRange(RemoteAccess.GetRemoteAccess().Items);
        allItems.AddRange(OpticalDiscTools.GetOpticalDiscTools().Items);
        allItems.AddRange(OtherUtilities.GetOtherUtilities().Items);
        allItems.AddRange(PrivacySecurity.GetPrivacySecurity().Items);
        allItems.AddRange(DevelopmentApps.GetDevelopmentApps().Items);
        allItems.AddRange(RuntimesAndDependencies.GetRuntimesAndDependencies().Items);

        return new AppGroup
        {
            Name = "External Apps",
            FeatureId = "ExternalApps",
            Items = allItems
        };
    }
}
