#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.DependencyGraph;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Tools.TypeHierarchy;
using AiNetLinter.Mcp.Tools.TypeResolution;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// Registriert die sechs reinen Symbolgraph-Tools (<c>find_symbol</c>, <c>find_references</c>,
/// <c>get_impact</c>, <c>get_type_hierarchy</c>, <c>get_call_tree</c>, <c>dependency_graph</c>) an
/// der von <see cref="McpServerOptionsFactory"/> aufgebauten Tool-Collection. Aus
/// <see cref="McpServerOptionsFactory"/> ausgelagert, damit dessen eigener <c>AIContextFootprint</c>
/// (siehe <c>AiNetLinter.mdc</c>) nicht mit jedem neu registrierten Tool waechst. Jedes Lambda ist
/// zielgebunden: <c>targetType</c> und <c>targetPath</c> sind Pflicht und werden am gemeinsamen
/// <see cref="AnalysisToolCall"/> validiert.
/// </summary>
internal static class SymbolGraphToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die sechs Symbolgraph-Tools hinzu. Tools erreichen die
    /// residente Instanz ihres Keys per Lease-Closure - kein DI-Container
    /// (siehe <c>AiNetLinterRichtlinien.mdc</c> §2).
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        AddFindSymbol(tools, targetRoute);
        AddFindReferences(tools, targetRoute);
        AddGetCallTree(tools, targetRoute);
        AddGetImpact(tools, targetRoute);
        AddGetTypeHierarchy(tools, targetRoute);
        AddDependencyGraph(tools, targetRoute);
        AddResolveTypeOrigin(tools, targetRoute);
        AddFindImplementations(tools, targetRoute);
    }

    private static void AddFindSymbol(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string[]? namePatterns = null, string? namePattern = null, string? symbol = null, string? kind = null, int maxResults = 50, bool includeReferences = false, int maxResponseBytes = 0, CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => FindSymbolTool.ExecuteAsync(
                                new FindSymbolRequest(lease.Server, namePatterns, kind, maxResults, ct, namePattern, symbol)),
                             AssemblySessionCall: lease => AssemblyFindSymbolTool.ExecuteAsync(
                                 lease,
                                 new AssemblyFindSymbolRequest(
                                     FindSymbolTool.NormalizeNamePatterns(namePatterns, namePattern, symbol).ToArray(),
                                     kind,
                                     maxResults,
                                     includeReferences),
                                 ct),
                             ExpandAssemblyReferences: includeReferences,
                             MaxResponseBytes: maxResponseBytes),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("find_symbol", FindSymbolDescription)));
    }

    private const string FindSymbolDescription =
        "Wann nutzen: Fundstelle(n) von C#-Symbolen per Namens-Substring finden, wenn der " +
        "exakte Ort unbekannt ist. namePatterns: Array von Namens-Mustern oder namePattern als " +
        "String-Alias fuer genau ein Muster; symbol bleibt ein kompatibler String-Alias. " +
        "Batch loest N sequentielle Calls ab, max. 10 pro Call, z. B. namePatterns: [\"Greeter\"] " +
        "oder namePattern: \"Greeter\". " +
        "kind: optionaler Typfilter (Class, Record, Method, Property, Interface, Struct, Enum; " +
        "deutsche und englische Werte). maxResults: Begrenzung der Trefferliste (Default 50). " +
        "includeReferences (Default false): bei targetType=assembly auch die bounded Referenz-Assemblies " +
        "durchsuchen und Herkunft/Completeness in structuredContent ausgeben. " +
        "Bei 0 C#-Treffern Hinweis auf Textfunde in Nicht-C#-Dateien (Fallback search_pattern). " +
        "Liefert strukturierte FindSymbolBatchDto in structuredContent.";

    private static void AddFindReferences(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? symbolIdentifier = null, string? symbol = null, int maxResults = 50, int depth = 1, bool includeReferences = false, CancellationToken ct = default) =>
            {
                var effectiveIdentifier = !string.IsNullOrWhiteSpace(symbolIdentifier) ? symbolIdentifier : symbol;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => FindReferencesTool.ExecuteAsync(lease.Server, effectiveIdentifier, maxResults, depth, ct),
                            AssemblySessionCall: lease => AssemblyFindReferencesTool.ExecuteAsync(
                                lease,
                                new AssemblyFindReferencesRequest(effectiveIdentifier, maxResults, depth, includeReferences),
                                ct),
                            ExpandAssemblyReferences: includeReferences),
                        ct));
            },
            McpToolRegistrationOptions.TargetedReadOnlyTool("find_references", FindReferencesDescription)));
    }

    private const string FindReferencesDescription =
        "Wann nutzen: alle Aufrufstellen eines C#-Symbols finden, optional transitiv. " +
        "symbolIdentifier (oder Alias symbol): \"M:Namespace.Klasse.Methode\" oder \"Datei.cs:42:10\" oder " +
        "\"Datei.cs:42\" (Zeile ohne Spalte — bei mehreren Symbolen auf der Zeile liefert das " +
        "Ergebnis eine Kandidatenliste statt eines Treffers) oder \"Klasse.Methode\". " +
        "maxResults: Begrenzung der Trefferliste (Default 50). " +
        "depth (Default 1, hard cap 3) liefert immer structuredContent.callSites plus " +
        "completeness mit Tiefe, Herkunft und getrennten Trunkierungsgruenden; die " +
        "Traversierung ist hart auf 200 besuchte Knoten begrenzt. includeReferences (Default false): " +
        "bei targetType=assembly bounded Referenz-Assemblies einbeziehen und partielle Diagnosen " +
        "sowie Herkunft in structuredContent ausgeben.";

    private static void AddGetCallTree(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? symbolIdentifier = null, string? symbol = null, int depth = 2, string? format = null, int topN = 10, string? direction = null, bool includeReferences = false, bool includeBcl = false, CancellationToken ct = default) =>
            {
                var effectiveIdentifier = !string.IsNullOrWhiteSpace(symbolIdentifier) ? symbolIdentifier : symbol;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetCallTreeTool.ExecuteAsync(lease.Server, new GetCallTreeInput(effectiveIdentifier, depth, format, topN, direction, IncludeBcl: includeBcl), ct),
                            AssemblySessionCall: lease => AssemblyGetCallTreeTool.ExecuteAsync(
                                lease,
                                new AssemblyGetCallTreeRequest(
                                    new GetCallTreeInput(effectiveIdentifier, depth, format, topN, direction, IncludeBcl: includeBcl),
                                    includeReferences),
                                ct),
                            ExpandAssemblyReferences: includeReferences),
                        ct));
            },
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_call_tree", GetCallTreeDescription)));
    }

    private const string GetCallTreeDescription =
        "Wann nutzen: echten Aufrufer- oder Aufgerufene-Baum eines C#-Symbols sehen (wer ruft " +
        "dieses Symbol auf bzw. wen ruft es auf), transitiv als Eltern-Kind-Struktur. " +
        "symbolIdentifier (oder Alias symbol): Format wie find_references (\"M:Namespace.Klasse.Methode\", \"Datei.cs:Zeile:Spalte\", \"Klasse.Methode\"). " +
        "depth: Traversierungstiefe (Default 2, hard cap 5). format: \"ascii\" (Default) oder " +
        "\"mermaid\" (flowchart TD). direction: \"incoming\" (Default: wer ruft das Symbol auf), " +
        "\"outgoing\" (wen ruft das Symbol auf) oder \"both\" (beide Richtungen abwechselnd). " +
        "topN: Fan-Out-Begrenzung pro Ebene (Default 10). Traversierung ist hart auf 250 Knoten begrenzt. " +
        "includeReferences (Default false): bei targetType=assembly bounded Referenz-Assemblies " +
        "einbeziehen und Herkunft/partielle Diagnosen im Ergebnis ausgeben. " +
        "includeBcl (Default false): bei direction=outgoing auch BCL-/Framework-Symbole (z. B. System.*) als Leaves einbeziehen.";

    private static void AddGetImpact(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? gitRef = null, string? symbolIdentifier = null, string? symbol = null, int maxResults = 50, int depth = 1,
                string? detailLevel = null,
                int maxChangedSymbols = ChangeContextContract.DefaultMaxChangedSymbols,
                int maxTestsPerSymbol = ChangeContextContract.DefaultMaxTestsPerSymbol,
                CancellationToken ct = default) =>
            {
                var effectiveIdentifier = !string.IsNullOrWhiteSpace(symbolIdentifier) ? symbolIdentifier : symbol;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(ProjectCall: lease => GetImpactTool.ExecuteAsync(
                            lease.Server,
                            new GetImpactInput(gitRef, effectiveIdentifier, maxResults, depth, detailLevel, maxChangedSymbols, maxTestsPerSymbol),
                            ct),
                            AssemblySessionCall: lease => GetImpactTool.ExecuteAsync(
                                lease.Server,
                                new GetImpactInput(gitRef, effectiveIdentifier, maxResults, depth, detailLevel, maxChangedSymbols, maxTestsPerSymbol),
                                ct),
                            ExpandAssemblyReferences: true),
                        ct));
            },
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_impact", GetImpactDescription)));
    }

    private const string GetImpactDescription =
        "Wann nutzen: pruefen, was eine geplante oder bereits gemachte Aenderung betrifft. " +
        "Ohne gitRef/symbolIdentifier: uncommittete lokale Aenderungen (Default). Sonst gitRef (Commit-Ref) " +
        "ODER symbolIdentifier (oder Alias symbol; Format wie find_references) angeben, nie beide. " +
        "Bei targetType='assembly' ist nur symbolIdentifier zulaessig; gitRef und leerer Aufruf " +
        "werden als recoverable InvalidArgument beantwortet. " +
        "detailLevel: 'callers' [Default] oder 'change-context' (nur im Git-Diff-Modus zulaessig: " +
        "liefert geaenderte Symbole, Call-Sites, zugeordnete Tests, diffbezogene Violations und dotnet test Filter). " +
        "maxResults: Limit der Trefferliste (Default 50). depth: Traversierungstiefe im Symbol-Branch (Default 1, hard cap 3, hart begrenzt auf 200 besuchte Knoten). " +
        "maxChangedSymbols: Begrenzung geaenderter Symbole im change-context (Default 20, Cap 100). " +
        "maxTestsPerSymbol: Begrenzung der Tests je Symbol (Default 10, Cap 50).";

    private static void AddGetTypeHierarchy(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? symbolIdentifier = null, string? symbol = null, int maxResults = GetTypeHierarchyTool.DefaultMaxResults, CancellationToken ct = default) =>
            {
                var effectiveIdentifier = !string.IsNullOrWhiteSpace(symbolIdentifier) ? symbolIdentifier : symbol;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, effectiveIdentifier, maxResults, ct),
                            AssemblySessionCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, effectiveIdentifier, maxResults, ct)),
                        ct));
            },
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_type_hierarchy", GetTypeHierarchyDescription)));
    }

    private const string GetTypeHierarchyDescription =
        "Wann nutzen: Vererbungs- und Interface-Hierarchie eines C#-Typs analysieren (Basisklassen, " +
        "implementierte Interfaces, abgeleitete/implementierende Typen, heuristische DI-Registrierungen). " +
        "symbolIdentifier (oder Alias symbol): \"T:Namespace.Klasse\", \"Datei.cs:10:5\", \"Datei.cs:10\" " +
        "(Zeile ohne Spalte) oder \"Klasse\". maxResults: Begrenzung der abgeleiteten/implementierenden " +
        "Typen (Default 50).";

    private static void AddDependencyGraph(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? filePath = null, string? symbolIdentifier = null, string? symbol = null, string? direction = null,
                int depth = 1, int maxResults = 50, CancellationToken ct = default) =>
            {
                var effectiveIdentifier = !string.IsNullOrWhiteSpace(symbolIdentifier) ? symbolIdentifier : symbol;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => DependencyGraphTool.ExecuteAsync(lease.Server, new DependencyGraphInput(filePath, effectiveIdentifier, direction, depth, maxResults), ct),
                            AssemblySessionCall: lease => DependencyGraphTool.ExecuteAsync(lease.Server, new DependencyGraphInput(filePath, effectiveIdentifier, direction, depth, maxResults), ct)),
                        ct));
            },
            McpToolRegistrationOptions.TargetedReadOnlyTool("dependency_graph", DependencyGraphDescription)));
    }

    private const string DependencyGraphDescription =
        "Wann nutzen: Ermitteln, welche Dateien/Typen von einer Datei oder einem Typ abhaengen " +
        "(echte SemanticModel-Typreferenzen, nicht nur using-Direktiven) — beantwortet 'wer haengt von X " +
        "ab' direkt statt mehrerer find_references-Umwege. filePath (ganze Datei) ODER " +
        "symbolIdentifier (ein Typ, engerer Scope) angeben, nie beide — symbolIdentifier-Format wie " +
        "find_references. direction: \"incoming\", \"outgoing\" oder \"both\" (Default). depth: " +
        "Traversierungstiefe (Default 1, hard cap 3, max. 150 besuchte Dateien). maxResults: " +
        "Begrenzung der angezeigten Kanten (Default 50).";

    private static void AddResolveTypeOrigin(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string typeName, CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => ResolveTypeOriginTool.ExecuteProjectAsync(lease.Server, typeName, ct),
                            AssemblySessionCall: lease => ResolveTypeOriginTool.ExecuteAssemblyAsync(lease, typeName, ct)),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("resolve_type_origin", ResolveTypeOriginDescription)));
    }

    private const string ResolveTypeOriginDescription =
        "Wann nutzen: Ermittelt zu einem angegebenen Typnamen (z. B. 'IDataProvider' oder 'Vendor.Data.BaseCommand') " +
        "sofort die definierende Assembly (Name und Festplatten-Dateipfad der DLL) sowie den vollqualifizierten Typnamen " +
        "und Symbol-Kind ueber Roslyn-Metadatenreferenzen. Unterstuetzt sowohl targetType='project' als auch targetType='assembly'.";

    private static void AddFindImplementations(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? symbolIdentifier = null, string? symbol = null, int maxResults = FindImplementationsTool.DefaultMaxResults, CancellationToken ct = default) =>
            {
                var effectiveIdentifier = !string.IsNullOrWhiteSpace(symbolIdentifier) ? symbolIdentifier : symbol;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => FindImplementationsTool.ExecuteAsync(lease.Server, effectiveIdentifier, maxResults, ct),
                            AssemblySessionCall: lease => FindImplementationsTool.ExecuteAsync(lease.Server, effectiveIdentifier, maxResults, ct)),
                        ct));
            },
            McpToolRegistrationOptions.TargetedReadOnlyTool("find_implementations", FindImplementationsDescription)));
    }

    private const string FindImplementationsDescription =
        "Wann nutzen: Findet konkrete Implementierungen und Overrides von Interfaces, abstrakten Klassen, " +
        "virtuellen Methoden oder Properties in Quellcode-Projekten (targetType='project') oder dekompilierten " +
        "Assemblies (targetType='assembly'). Liefert Typ, Member, Status (concrete/abstract/virtual) und Zeilenposition. " +
        "symbolIdentifier (oder Alias symbol): Format wie find_references (\"M:Namespace.Klasse.Methode\", \"IInterface\", \"BaseClass.Method\"). " +
        "maxResults: Begrenzung der Trefferliste (Default 50).";
}

