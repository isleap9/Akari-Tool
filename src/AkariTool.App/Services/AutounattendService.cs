using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using AkariTool.Tabs;

namespace AkariTool.Services
{
    /// <summary>
    /// One optional tweak the user can bake into the generated autounattend.xml.
    /// ScriptName references an embedded Scripts\*.ps1 resource; entries with a null
    /// ScriptName are implemented inline by the builder (e.g. clean Start Menu).
    /// UserScoped tweaks run in the per-user pass (HKCU), the rest run as SYSTEM.
    /// </summary>
    public sealed record UnattendTweakOption(
        string Id,
        string? ScriptName,
        string Name,
        string Description,
        bool UserScoped,
        bool DefaultOn);


    /// <summary>
    /// Generates an autounattend.xml based on the user's current Akari Tool
    /// selections — ported from Winhance's AutounattendXmlGeneratorService /
    /// AutounattendScriptBuilder pipeline and adapted to Akari's script-driven
    /// architecture.
    ///
    /// The XML embeds a single AkariTweaks.ps1 inside &lt;Extensions&gt;; Windows
    /// Setup extracts it during the specialize pass and runs it as SYSTEM. The
    /// script removes the selected Windows apps (BloatRemoval/Edge/OneDrive,
    /// with self-healing scheduled tasks), applies the selected system-wide
    /// Akari tweaks, and registers a logon task that re-runs itself with
    /// -UserCustomizations to apply per-user tweaks once for the first user.
    ///
    /// All embedded child scripts are Base64-encoded so arbitrary script content
    /// can never break the outer here-string, the CDATA section, or the XML.
    /// </summary>
    public partial class AutounattendService
    {
        private const string ScriptsDir  = @"C:\ProgramData\AkariTool\Unattend\Scripts";
        private const string LogPath     = @"C:\ProgramData\AkariTool\Unattend\Logs\AkariTweaks.txt";
        private const string MainScript  = @"C:\ProgramData\AkariTool\Unattend\Scripts\AkariTweaks.ps1";
        private const string MarkerKey   = @"Software\AkariTool"; // HKCU marker root
        private const string ReleasesUrl = "https://github.com/isleap9/Akari-Tool/releases/latest";
        public const string AkariAutounattendXmlUrl = "https://raw.githubusercontent.com/isleap9/Akari-Tool-Autounattend/main/autounattend.xml";

        private readonly ToolService _service;
        private static readonly Assembly AppAssembly = typeof(AutounattendService).Assembly;

        /// <summary>Test hook: load script content by name instead of embedded resources.</summary>
        internal Func<string, string>? ScriptContentOverride { get; set; }

        public AutounattendService(ToolService service) => _service = service;

        private void Log(string msg) => _service.Log($"[XML] {msg}");

        // ═════════════════════════════════════════════════════════════════
        //  Public API
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the autounattend.xml and writes it (UTF-8 without BOM — required
        /// by Windows Setup) to <paramref name="outputPath"/>. Throws on failure.
        /// </summary>
        public string GenerateToFile(
            string outputPath,
            IReadOnlyList<AppDefinition> selectedWindowsApps,
            IReadOnlyList<UnattendTweakOption> selectedTweaks)
        {
            Log($"Generating autounattend.xml — {selectedWindowsApps.Count} app(s), {selectedTweaks.Count} tweak(s)");

            var script = BuildAkariTweaksScript(selectedWindowsApps, selectedTweaks);

            if (script.Contains("]]>"))
                throw new InvalidOperationException(
                    "Generated script contains the CDATA terminator ']]>' and cannot be embedded safely.");

            var xml = Template.Replace("<!--SCRIPT_PLACEHOLDER-->", $"<![CDATA[{script}]]>");

            // Well-formedness validation (Winhance validates via PowerShell; XDocument is equivalent)
            XDocument.Parse(xml);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, xml, new UTF8Encoding(false));

            Log($"autounattend.xml written: {outputPath}");
            return outputPath;
        }

        // ═════════════════════════════════════════════════════════════════
        //  Script builder
        // ═════════════════════════════════════════════════════════════════

        private string BuildAkariTweaksScript(
            IReadOnlyList<AppDefinition> apps,
            IReadOnlyList<UnattendTweakOption> tweaks)
        {
            var sb = new StringBuilder();
            AppendHeader(sb);
            AppendLoggingSetup(sb);
            AppendHelperFunctions(sb);

            var systemTweaks = tweaks.Where(t => !t.UserScoped).ToList();
            var userTweaks   = tweaks.Where(t =>  t.UserScoped).ToList();

            // ── System-wide pass ───────────────────────────────────────────
            sb.AppendLine();
            sb.AppendLine("if (-not $UserCustomizations) {");
            sb.AppendLine();
            AppendScriptsDirectorySetup(sb);
            AppendAppRemovalSection(sb, apps);
            AppendSystemTweaksSection(sb, systemTweaks);
            AppendUserTweakExtraction(sb, userTweaks);
            if (tweaks.Any(t => t.Id == "clean-start-menu"))
                AppendCleanStartMenuSection(sb);
            AppendAkariInstallerShortcut(sb);
            if (userTweaks.Count > 0)
                AppendUserCustomizationsScheduledTask(sb);
            AppendCustomScriptPlaceholder(sb, "    ", "SYSTEM WIDE");
            sb.AppendLine("}");
            sb.AppendLine();

            // ── Per-user pass ──────────────────────────────────────────────
            sb.AppendLine("if ($UserCustomizations) {");
            sb.AppendLine();
            AppendUserDetectionBridge(sb);
            AppendUserTweaksSection(sb, userTweaks);
            AppendCustomScriptPlaceholder(sb, "            ", "USER SPECIFIC");
            AppendUserDetectionBridgeClosing(sb);

            AppendCompletionBlock(sb);
            return sb.ToString();
        }

    }
}
