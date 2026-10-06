// =============================================================================
// SettingIdDiff - the AKARI side of the SPIKE-03 setting-ID divergence report.
//
// WHY THIS EXISTS AS A COMPILED HOST (plan 01-04, Task 1)
// ---------------------------------------------------------------------------
// Windows PowerShell 5.1 is the only PowerShell on this machine (pwsh is not
// installed) and it cannot load a `net10.0-windows` assembly. So a committed
// script that needs to RUN .NET 10 code against AkariTool.Core.dll needs a
// compiled host. Do not "simplify" this back into a .ps1 that calls
// [Reflection.Assembly]::LoadFrom - it fails on the runtime, not on the code.
//
// WHAT THIS TOOL IS (per D-09)
// ---------------------------------------------------------------------------
// It reflects over the STATIC CATALOG FACTORIES declared by AkariTool.Core and
// reads each emitted setting's `Id`. The Akari side therefore cannot drift from
// the catalog: a renamed factory, a factory made non-public, or a factory made
// nested changes this tool's output or fails it loudly - it can never silently
// drop a domain.
//
// THE SEAM IS 15 ENTRY POINTS, NOT 11 - and 31 is the wrong number too.
// ---------------------------------------------------------------------------
//   11  `public static IReadOnlyList<SettingGroup> Build()` on top-level classes
//   4  `public static AppGroup Get*()`           on top-level classes
//   --  ENTRY POINTS                                          = 15
//   16  `public static AppGroup Get*()`      on classes NESTED inside
//       ExternalAppCatalog - the category factories its aggregate unions
//   --  candidates a return-type filter alone would find        = 31
//
// SoftwareApps (4 of the 15) has NO Build() method at all and is roughly 55% of
// Akari's setting IDs, so an 11-entry enumeration silently omits the largest
// domain. Conversely the 16 nested factories are the COMPONENTS of one aggregate,
// not sixteen catalogs: enumerating them alongside the aggregate would count
// their IDs twice.
//
// Hence the one structural filter that makes discovery, the expected count and
// the guard agree: keep a factory only when its declaring type is NOT nested
// (`DeclaringType.IsNested == false`). That is a property of the assembly's
// shape, not a naming convention, so Phase 3's namespace alignment cannot
// invalidate it. Excluding the components is then PROVED lossless by a multiset
// equality against their aggregate - see VerifyComponentCoverage.
//
// ---------------------------------------------------------------------------
// !!! PHASE 3 WARNING - READ BEFORE EDITING THE DERIVATION RULE !!!
// ---------------------------------------------------------------------------
// All eleven Build() types physically live in
// `src/AkariTool.Core/Features/<Domain>/Catalogs/` but declare `AkariTool.Tabs.*`
// namespaces. Phase 3's ARCH-09 namespace alignment WILL move those types. When
// it does, this tool must be updated AT ITS NAMESPACE-DERIVATION RULE
// (`AttributeDomain`) - by re-deriving from wherever the domain segment ends up,
// NEVER by adding an entry to a list. There is deliberately no list to add to.
// The same applies to the AppGroup arm, which is derived by RETURN TYPE and so
// survives the move untouched.
//
// NO HAND-WRITTEN CATALOG LIST EXISTS IN THIS FILE, BY DESIGN. A hard-coded list
// is the "second place to update" failure this milestone exists to prevent, and
// such a list already exists as the anti-analog in
// `tests/AkariTool.Core.Tests/Features/SettingCatalogValidatorTests.cs`
// (the `Tabs(string key)` switch). Do not copy it, extend it, or depend on it.
//
// NOT A GATE ON CONTENT (per D-10). This tool fails on SHAPE - a missing entry
// point, an empty domain, an aggregate that does not cover its components. It
// does not judge IDs, and a duplicate ID is RECORDED, never collapsed.
// =============================================================================

using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SettingIdDiff;

/// <summary>
/// Expected number of catalog entry points after the non-nested filter.
/// This constant and the discovery rule are mutually consistent: the rule yields
/// exactly this many factories, which is what makes a mismatch legible.
/// </summary>
internal static class SettingIdDiffTool
{
    /// <summary>The return types that mark a static, zero-parameter catalog factory.</summary>
    private const string SettingGroupTypeName = "AkariTool.Core.Features.Common.Models.SettingGroup";

    private const string AppGroupTypeName = "AkariTool.Tabs.AppGroup";

    /// <summary>
    /// Domain attributed to every factory whose return type is <c>AppGroup</c>,
    /// derived by return type rather than by any name list, so it survives
    /// Phase 3's namespace alignment.
    /// </summary>
    private const string AppGroupDomain = "SoftwareApps";

    /// <summary>Output directory, relative to the solution root.</summary>
    private const string DataDirRelative = "tools/data";

    /// <summary>
    /// An ID that leaves this character class is rejected before it reaches a
    /// file. The Akari side is our own source, but the same discipline as the
    /// Winhance parser keeps one-ID-per-line a real invariant of the format.
    /// </summary>
    private static readonly Regex IdPattern = new(@"^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine(
                "USAGE: SettingIdDiff <repo-relative-path-to-AkariTool.Core.dll>");
            Console.Error.WriteLine(
                "  This tool takes exactly one argument and never searches for the assembly: " +
                "a tool that silently picks a different DLL produces a confidently wrong report.");
            return 2;
        }

        var dllPath = Path.GetFullPath(args[0]);

        // First line of output is the resolved absolute path, so a run is attributable.
        Console.WriteLine(dllPath);

        if (!File.Exists(dllPath))
        {
            Console.Error.WriteLine(
                $"FATAL: AkariTool.Core.dll not found at '{dllPath}'.");
            Console.Error.WriteLine(
                "  Build Core first (tools\\run-tests.ps1 -Mode Baseline does a /t:Rebuild). " +
                "This tool deliberately has no ProjectReference to Core, so it will never build it.");
            return 2;
        }

        var solutionRoot = FindSolutionRoot(dllPath);
        if (solutionRoot is null)
        {
            Console.Error.WriteLine(
                $"FATAL: could not derive the solution root from '{dllPath}' " +
                "(no AkariTool.sln in any parent directory).");
            Console.Error.WriteLine(
                "  Output is always written to <solution root>\\tools\\data; " +
                "this tool never guesses a destination directory.");
            return 2;
        }

        var dataDir = Path.Combine(solutionRoot, DataDirRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(dataDir);

        var assembly = Assembly.LoadFrom(dllPath);

        // AkariTool.Core declares ONE interface that references WinAppSDK
        // (IDispatcherService -> Microsoft.UI.Dispatching), so asking for its
        // exported types drags in projection assemblies Core's own output directory
        // does not carry. Loading this tool is a measurement of the CATALOG, not of
        // Core's reference graph, so an individual unloadable type is tolerated -
        // but never silently: the skipped set is reported on stdout, and a skipped
        // CATALOG factory still trips the entry-point guard below.
        var loadableTypes = GetLoadableTypes(dllPath, assembly);

        var entryPoints = EnumerateCatalogEntryPoints(loadableTypes);
        var nested = entryPoints.NestedFactories;

        // ── Guard: the seam must be exactly EXPECTED_ENTRY_POINT_COUNT ─────────
        if (entryPoints.EntryPoints.Count != ExpectedEntryPointCount)
        {
            Console.Error.WriteLine(
                $"FATAL: discovered {entryPoints.EntryPoints.Count} catalog entry points, " +
                $"expected {ExpectedEntryPointCount} " +
                $"(pre-filter candidates: {entryPoints.PreFilterCandidateCount}).");
            Console.Error.WriteLine(
                "  A pre-filter count of " + (ExpectedEntryPointCount + nested.Count) +
                " against " + ExpectedEntryPointCount +
                " means the nested aggregate COMPONENTS leaked into the entry-point set.");
            Console.Error.WriteLine("  Discovered entry points:");
            foreach (var name in entryPoints.EntryPoints.Select(DescribeFactory))
            {
                Console.Error.WriteLine($"    {name}");
            }
            Console.Error.WriteLine("  Excluded nested factories:");
            foreach (var name in nested.Select(DescribeFactory))
            {
                Console.Error.WriteLine($"    {name}");
            }
            Console.Error.WriteLine(
                "  A factory may have been renamed, made non-public, nested, or changed shape.");
            return 1;
        }

        // ── Walk every entry point, grouped by mechanically derived domain ─────
        var domains = new SortedDictionary<string, DomainResult>(StringComparer.Ordinal);
        var rejectedIds = new List<string>();

        foreach (var factory in entryPoints.EntryPoints)
        {
            var domainName = AttributeDomain(factory);
            if (!domains.TryGetValue(domainName, out var domain))
            {
                domain = new DomainResult(domainName);
                domains[domainName] = domain;
            }

            domain.EntryPoints.Add(DescribeFactory(factory));

            var ids = factory.ReturnsAppGroup
                ? WalkAppGroups(factory.Invoke())
                : WalkSettingGroups(factory.Invoke());

            foreach (var id in ids)
            {
                if (!NormaliseForOutput(id, rejectedIds))
                {
                    continue;
                }

                domain.Ids.Add(id);
            }
        }

        // ── Guard: no derived domain may be empty ─────────────────────────────
        var emptyDomains = domains.Where(kvp => kvp.Value.Ids.Count == 0).Select(kvp => kvp.Key).ToList();
        if (emptyDomains.Count > 0)
        {
            Console.Error.WriteLine(
                "FATAL: these derived domains yielded zero IDs: " + string.Join(", ", emptyDomains));
            Console.Error.WriteLine(
                "  A report that silently omitted a domain (SoftwareApps especially) must be impossible to produce.");
            return 1;
        }

        // ── Prove the nested-component exclusion is lossless ───────────────────
        var coverage = VerifyComponentCoverage(entryPoints);
        if (!coverage.Equal)
        {
            return 1;
        }

        // ── Emit ───────────────────────────────────────────────────────────────
        var totalIds = domains.Sum(kvp => kvp.Value.Ids.Count);
        var duplicates = domains
            .SelectMany(kvp => kvp.Value.Ids
                .GroupBy(id => id, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => new DuplicateId
                {
                    Domain = kvp.Key,
                    Id = g.Key,
                    Count = g.Count(),
                }))
            .OrderBy(d => d.Domain, StringComparer.Ordinal)
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .ToList();

        if (rejectedIds.Count > 0)
        {
            Console.Error.WriteLine(
                $"FATAL: {rejectedIds.Count} Akari ID(s) contained characters outside [A-Za-z0-9._-]:");
            foreach (var id in rejectedIds)
            {
                Console.Error.WriteLine($"    {id}");
            }
            return 1;
        }

        WriteDomainFiles(dataDir, domains);
        WriteJson(dataDir, entryPoints, coverage, domains, totalIds, duplicates);

        Console.WriteLine($"entryPoints: {entryPoints.EntryPoints.Count}");
        Console.WriteLine($"excludedNestedFactories: {nested.Count}");
        Console.WriteLine($"componentCoverage.equal: {coverage.Equal.ToString().ToLowerInvariant()}");
        Console.WriteLine($"domains: {domains.Count} ({string.Join(", ", domains.Keys)})");
        Console.WriteLine($"ids: {totalIds} (duplicate ids: {duplicates.Count})");
        Console.WriteLine($"dataDir: {dataDir}");
        return 0;
    }

    /// <summary>Discovery rule, expected count and guard, kept in one place.</summary>
    private const int ExpectedEntryPointCount = 15;

    private sealed record Factory(MethodInfo Method, Type DeclaringType, bool ReturnsAppGroup, bool IsNested)
    {
        public object Invoke() => Method.Invoke(null, null)
            ?? throw new InvalidOperationException(
                $"Factory '{DescribeFactory(this)}' returned null.");
    }

    private sealed record EntryPointScan(
        List<Factory> EntryPoints,
        List<Factory> NestedFactories,
        int PreFilterCandidateCount);

    private sealed record DomainResult(string Name)
    {
        public List<string> EntryPoints { get; } = [];

        public List<string> Ids { get; } = [];
    }

    private sealed record DuplicateId
    {
        public string Domain { get; init; } = string.Empty;

        public string Id { get; init; } = string.Empty;

        public int Count { get; init; }
    }

    private sealed record ComponentCoverage
    {
        public int NestedFactories { get; init; }

        public string Aggregate { get; init; } = string.Empty;

        public bool Equal { get; init; }

        public int ComponentIds { get; init; }

        public int AggregateIds { get; init; }

        public List<string> OnlyInComponents { get; init; } = [];

        public List<string> OnlyInAggregate { get; init; } = [];
    }

    private static string DescribeFactory(Factory factory) =>
        $"{factory.DeclaringType.FullName}.{factory.Method.Name}" +
        $" -> {(factory.ReturnsAppGroup ? "AppGroup" : "IReadOnlyList<SettingGroup>")}" +
        $"{(factory.IsNested ? "  [NESTED - aggregate component, excluded]" : string.Empty)}";

    /// <summary>
    /// The one discovery rule: public type, public static method, zero parameters,
    /// catalog return type. Split into entry points (non-nested declaring type) and
    /// aggregate components (nested declaring type) so the exclusion is visible.
    /// </summary>
    private static EntryPointScan EnumerateCatalogEntryPoints(IReadOnlyList<Type> types)
    {
        var entryPoints = new List<Factory>();
        var nested = new List<Factory>();
        var preFilter = 0;

        foreach (var type in types)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.GetParameters().Length != 0)
                {
                    continue;
                }

                if (method.IsSpecialName)
                {
                    continue;
                }

                if (!TryGetCatalogReturnType(method.ReturnType, out var returnsAppGroup))
                {
                    continue;
                }

                preFilter++;
                var factory = new Factory(method, type, returnsAppGroup, type.IsNested);
                if (type.IsNested)
                {
                    nested.Add(factory);
                }
                else
                {
                    entryPoints.Add(factory);
                }
            }
        }

        entryPoints.Sort(static (a, b) => string.CompareOrdinal(
            $"{a.DeclaringType.FullName}.{a.Method.Name}",
            $"{b.DeclaringType.FullName}.{b.Method.Name}"));
        nested.Sort(static (a, b) => string.CompareOrdinal(
            $"{a.DeclaringType.FullName}.{a.Method.Name}",
            $"{b.DeclaringType.FullName}.{b.Method.Name}"));

        return new EntryPointScan(entryPoints, nested, preFilter);
    }

    private static bool TryGetCatalogReturnType(Type returnType, out bool returnsAppGroup)
    {
        returnsAppGroup = returnType.FullName == AppGroupTypeName;
        if (returnsAppGroup)
        {
            return true;
        }

        return returnType.IsGenericType
            && returnType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            && returnType.GetGenericArguments().Length == 1
            && returnType.GetGenericArguments()[0].FullName == SettingGroupTypeName;
    }

    /// <summary>
    /// Excluding the nested category factories is only legitimate if their IDs are
    /// reachable through the aggregate. Prove it by multiset (count, not set)
    /// equality - multiplicity matters, because a duplicated ID would hide behind
    /// set semantics.
    ///
    /// The aggregate is found MECHANICALLY: it is the entry point declared on the
    /// same type that declares the nested component factories (the nested classes
    /// live inside the aggregate's partial class). No factory name is spelled here.
    /// </summary>
    private static ComponentCoverage VerifyComponentCoverage(EntryPointScan scan)
    {
        if (scan.NestedFactories.Count == 0)
        {
            Console.Error.WriteLine(
                "FATAL: no nested aggregate components were discovered, so the coverage proof is vacuous. " +
                "If the component factories were flattened into separate top-level types, this tool's " +
                "non-nested filter no longer separates a catalog from a component and the derivation rule " +
                "must be revisited.");
            return new ComponentCoverage { Equal = false };
        }

        var componentOwners = scan.NestedFactories
            .Select(f => f.DeclaringType.DeclaringType)
            .Where(t => t is not null)
            .Distinct()
            .ToList();

        if (componentOwners.Count != 1)
        {
            Console.Error.WriteLine(
                "FATAL: the nested component factories are declared inside " +
                $"{componentOwners.Count} different owning types ({string.Join(", ", componentOwners.Select(t => t!.FullName))}), " +
                "so there is no single aggregate to prove them against. " +
                "The exclusion rule needs re-deriving; do not guess which aggregate is meant.");
            return new ComponentCoverage { Equal = false };
        }

        var owner = componentOwners[0]!;

        var aggregates = scan.EntryPoints
            .Where(f => f.ReturnsAppGroup && f.DeclaringType == owner)
            .ToList();

        if (aggregates.Count != 1)
        {
            Console.Error.WriteLine(
                $"FATAL: expected exactly one AppGroup factory on the aggregate owner '{owner.FullName}', " +
                $"found {aggregates.Count}. The coverage proof needs one aggregate.");
            return new ComponentCoverage { Equal = false };
        }

        // Ids already passed validation on the entry-point walk; re-validate only
        // to keep the two sides measured identically.
        var componentIds = scan.NestedFactories
            .SelectMany(factory => WalkAppGroups(factory.Invoke()))
            .Where(id => NormaliseForOutput(id, []))
            .ToList();
        var aggregateIds = WalkAppGroups(aggregates[0].Invoke())
            .Where(id => NormaliseForOutput(id, []))
            .ToList();

        var onlyComponents = MultisetDifference(componentIds, aggregateIds);
        var onlyAggregate = MultisetDifference(aggregateIds, componentIds);
        var equal = onlyComponents.Count == 0 && onlyAggregate.Count == 0;

        if (!equal)
        {
            Console.Error.WriteLine(
                "FATAL: the nested component factories' IDs are NOT equal to their aggregate's. " +
                "Excluding them is therefore lossy and the fix belongs in the derivation rule, not in the report.");
            Console.Error.WriteLine($"  aggregate: {DescribeFactory(aggregates[0])}");
            Console.Error.WriteLine($"  present in components, absent from aggregate ({onlyComponents.Count}):");
            foreach (var id in onlyComponents)
            {
                Console.Error.WriteLine($"    {id}");
            }
            Console.Error.WriteLine($"  present in aggregate, absent from components ({onlyAggregate.Count}):");
            foreach (var id in onlyAggregate)
            {
                Console.Error.WriteLine($"    {id}");
            }
        }
        else
        {
            Console.WriteLine(
                $"componentCoverage: {scan.NestedFactories.Count} nested factories are covered exactly by " +
                $"{DescribeFactory(aggregates[0])} ({componentIds.Count} IDs, multiset equal)");
        }

        return new ComponentCoverage
        {
            NestedFactories = scan.NestedFactories.Count,
            Aggregate = DescribeFactory(aggregates[0]),
            Equal = equal,
            ComponentIds = componentIds.Count,
            AggregateIds = aggregateIds.Count,
            OnlyInComponents = onlyComponents,
            OnlyInAggregate = onlyAggregate,
        };
    }

    /// <summary>Multiset difference: multiplicity-aware, so a duplicate cannot hide.</summary>
    private static List<string> MultisetDifference(List<string> left, List<string> right)
    {
        var remaining = new List<string>(right);
        var result = new List<string>();
        foreach (var id in left)
        {
            var index = remaining.FindIndex(candidate => string.Equals(candidate, id, StringComparison.Ordinal));
            if (index >= 0)
            {
                remaining.RemoveAt(index);
            }
            else
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>
    /// Read a <c>IReadOnlyList&lt;SettingGroup&gt;</c> without a compile-time reference to
    /// Core: each group's <c>Settings</c>, then each item's <c>Id</c>.
    ///
    /// There is NO group-level <c>Id</c>: identity is <c>FeatureId</c> plus per-item
    /// <c>Id</c>, and reading the wrong member would silently produce an empty report.
    ///
    /// This output is deliberately shaped for Phase 2's validator test to consume
    /// unchanged. SettingCatalogValidator.Validate(IEnumerable&lt;SettingGroup&gt;)
    /// already enforces global ID uniqueness, registry-path shape and ComboBox
    /// mapping integrity. DO NOT call it from here: that would turn this
    /// measurement into a partial BUG-02 implementation and move another phase's
    /// requirement into this one.
    /// </summary>
    private static List<string> WalkSettingGroups(object? returned)
    {
        var ids = new List<string>();
        if (returned is not System.Collections.IEnumerable groups)
        {
            throw new InvalidOperationException(
                $"Expected an enumerable of SettingGroup, got '{returned?.GetType().FullName ?? "null"}'.");
        }

        foreach (var group in groups)
        {
            if (group is null)
            {
                continue;
            }

            var settings = group.GetType().GetProperty("Settings")?.GetValue(group)
                as System.Collections.IEnumerable
                ?? throw new InvalidOperationException(
                    $"SettingGroup '{group.GetType().FullName}' has no readable Settings collection.");

            foreach (var setting in settings)
            {
                if (setting is null)
                {
                    continue;
                }

                var id = setting.GetType().GetProperty("Id")?.GetValue(setting) as string;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    /// <summary>
    /// Read a single <c>AppGroup</c>: its <c>Items</c>, then each item's <c>Id</c>.
    /// Like <see cref="WalkSettingGroups"/>, there is no group-level Id.
    /// </summary>
    private static List<string> WalkAppGroups(object? returned)
    {
        var ids = new List<string>();
        if (returned is null)
        {
            throw new InvalidOperationException("An AppGroup factory returned null.");
        }

        var items = returned.GetType().GetProperty("Items")?.GetValue(returned)
            as System.Collections.IEnumerable
            ?? throw new InvalidOperationException(
                $"AppGroup '{returned.GetType().FullName}' has no readable Items collection.");

        foreach (var item in items)
        {
            if (item is null)
            {
                continue;
            }

            var id = item.GetType().GetProperty("Id")?.GetValue(item) as string;
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>
    /// Domain attribution, derived - never listed.
    ///
    ///   AppGroup-returning factory          -> the AppGroup domain (return-type rule)
    ///   IReadOnlyList&lt;SettingGroup&gt;-returning factory -> the LAST segment of its
    ///                                               declaring namespace
    ///                                               (AkariTool.Tabs.Gaming -> Gaming)
    ///
    /// The AppGroup arm is attributed by RETURN TYPE on purpose: those factories
    /// declare the bare <c>AkariTool.Tabs</c> namespace with no domain segment, and
    /// a return-type rule is exactly what keeps them correct after Phase 3 moves the
    /// namespaces around.
    /// </summary>
    private static string AttributeDomain(Factory factory)
    {
        if (factory.ReturnsAppGroup)
        {
            return AppGroupDomain;
        }

        var segments = (factory.DeclaringType.Namespace ?? string.Empty)
            .Split('.', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length == 0
            ? throw new InvalidOperationException(
                $"Factory '{DescribeFactory(factory)}' declares no namespace, so no domain can be derived from it.")
            : segments[^1];
    }

    /// <summary>
    /// Validate and normalise an ID for the one-ID-per-line output format.
    /// Returns false (and records the offending value) rather than emitting a line
    /// that would corrupt the format.
    /// </summary>
    private static bool NormaliseForOutput(string id, List<string> rejects)
    {
        var trimmed = id.Trim();
        if (trimmed.Length == 0 || !IdPattern.IsMatch(trimmed))
        {
            rejects.Add(id);
            return false;
        }

        return true;
    }

    private static void WriteDomainFiles(
        string dataDir,
        SortedDictionary<string, DomainResult> domains)
    {
        foreach (var (name, domain) in domains)
        {
            var sorted = domain.Ids
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            var path = Path.Combine(dataDir, $"akari-{name.ToLowerInvariant()}.ids.txt");
            WriteAllTextNoBom(path, string.Join("\n", sorted) + "\n");
        }
    }

    private static void WriteJson(
        string dataDir,
        EntryPointScan scan,
        ComponentCoverage coverage,
        SortedDictionary<string, DomainResult> domains,
        int totalIds,
        List<DuplicateId> duplicates)
    {
        var payload = new
        {
            generatedOn = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            entryPoints = scan.EntryPoints.Count,
            preFilterCandidateCount = scan.PreFilterCandidateCount,
            expectedEntryPointCount = ExpectedEntryPointCount,
            excludedNestedFactories = scan.NestedFactories.Select(DescribeFactory).ToList(),
            componentCoverage = new
            {
                nestedFactories = coverage.NestedFactories,
                aggregate = coverage.Aggregate,
                equal = coverage.Equal,
                componentIds = coverage.ComponentIds,
                aggregateIds = coverage.AggregateIds,
                onlyInComponents = coverage.OnlyInComponents,
                onlyInAggregate = coverage.OnlyInAggregate,
            },
            domains = domains.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    entryPoints = kvp.Value.EntryPoints,
                    ids = kvp.Value.Ids.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                },
                StringComparer.Ordinal),
            totals = new
            {
                domains = domains.Count,
                ids = totalIds,
                uniqueIds = domains.Sum(kvp => kvp.Value.Ids.Distinct(StringComparer.Ordinal).Count()),
            },
            duplicates,
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        WriteAllTextNoBom(
            Path.Combine(dataDir, "akari-setting-ids.json"),
            JsonSerializer.Serialize(payload, options) + "\n");
    }

    /// <summary>UTF-8 without BOM, LF-terminated: byte-identical across runs.</summary>
    private static void WriteAllTextNoBom(string path, string content) =>
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    /// <summary>
    /// The assembly's exported types, tolerating individual types whose reference
    /// graph cannot be resolved in this host.
    ///
    /// The tolerance is narrow and reported: only a type whose LOAD failed is
    /// dropped, never a type whose shape did not match. No catalog factory can be
    /// lost silently, because the entry-point guard would then find fewer than
    /// EXPECTED_ENTRY_POINT_COUNT.
    /// </summary>
    /// <summary>
    /// The assembly's PUBLIC type names, read from its METADATA rather than from
    /// the runtime type system.
    ///
    /// This is the whole reason the tool needs no PackageReference and no rebuild
    /// of Core. <c>Assembly.GetExportedTypes()</c> resolves every public type's
    /// reference graph up front, and AkariTool.Core declares exactly one type
    /// (IDispatcherService, via <c>Microsoft.UI.Dispatching</c>) whose projection
    /// assembly is not copied to Core's own output directory - so the wholesale call
    /// throws. Reading the name list from metadata resolves nothing, and
    /// <c>GetType</c> is then asked for each name individually, so only the types
    /// this tool actually reflects over are ever loaded.
    ///
    /// A type that still cannot be loaded is reported, never silently dropped - and
    /// a catalog factory lost this way would trip the entry-point guard.
    /// </summary>
    private static IReadOnlyList<Type> GetLoadableTypes(string assemblyPath, Assembly assembly)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();

        var names = new List<string>();
        foreach (var handle in reader.TypeDefinitions)
        {
            var definition = reader.GetTypeDefinition(handle);
            var attributes = definition.Attributes;

            var isExported = (attributes & TypeAttributes.VisibilityMask) switch
            {
                TypeAttributes.Public => true,
                TypeAttributes.NestedPublic => true,
                TypeAttributes.NestedFamily => true,
                TypeAttributes.NestedFamORAssem => true,
                _ => false,
            };

            if (!isExported)
            {
                continue;
            }

            names.Add(ReadMetadataTypeName(reader, handle));
        }

        var loaded = new List<Type>();
        var failed = new List<string>();
        foreach (var name in names)
        {
            try
            {
                var type = assembly.GetType(name, throwOnError: false, ignoreCase: false);
                if (type is not null)
                {
                    loaded.Add(type);
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or TypeLoadException)
            {
                failed.Add(name);
            }
        }

        if (failed.Count > 0)
        {
            Console.WriteLine(
                $"note: {failed.Count} exported type(s) could not be loaded and were excluded: " +
                string.Join(", ", failed));
        }

        return loaded;
    }

    /// <summary>
    /// The runtime full name of a metadata type definition. A NESTED type carries an
    /// empty namespace in metadata and must be rendered as <c>Outer+Inner</c> - which
    /// is exactly the distinction the discovery rule turns on, so getting the name
    /// wrong here would make nested and top-level factories indistinguishable.
    /// </summary>
    private static string ReadMetadataTypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var definition = reader.GetTypeDefinition(handle);
        var simpleName = reader.GetString(definition.Name);
        var declaring = definition.GetDeclaringType();

        if (!declaring.IsNil)
        {
            return $"{ReadMetadataTypeName(reader, declaring)}+{simpleName}";
        }

        var ns = reader.GetString(definition.Namespace);
        return ns.Length == 0 ? simpleName : $"{ns}.{simpleName}";
    }

    /// <summary>
    /// Walk up from the assembly to the directory holding AkariTool.sln. Output
    /// location is derived from the project layout, never from a hard-coded
    /// absolute path - a path literal here would break every other checkout.
    /// </summary>
    private static string? FindSolutionRoot(string startPath)
    {
        var directory = new DirectoryInfo(
            File.Exists(startPath) ? Path.GetDirectoryName(startPath)! : startPath);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AkariTool.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}