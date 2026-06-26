# `--map skeleton` — Implementierungsplan

> **Ziel:** Einen neuen Map-Typ `skeleton` implementieren, der eine gesamte C#-Codebasis
> semantisch skelettiert. Das Ergebnis ist ein kompakter, für LLMs optimierter Markdown-Dump,
> der Typ-Strukturen, Member-Signaturen und Methoden-Metadaten (Throws/Uses) enthält —
> ohne Implementierungs-Rümpfe. Einsatz: Architektur-Audits (Naming-Drift, Code-Duplikate,
> Dependency-Drift) mit Modellen, bei denen jedes Token zählt.

---

## 1. Architektur-Entscheidungen

| Thema | Entscheidung | Begründung |
|:---|:---|:---|
| Roslyn-Tiefe | **Voller MSBuildWorkspace** (via `SourceFileCatalog.LoadAsync`) | Semantisches Modell nötig für präzise Typ-Auflösung von Felder in Methoden-Rümpfen |
| Stub-Inhalt | **Signatur + `// Throws: X \| Uses: IRepo`** | Reine Signaturen lassen LLMs blind für Kopplungen; ein vollständiger Rumpf kostet zu viele Tokens |
| Namespace-Filter | **v2** — in v1 werden alle Typen skelettiert | Einfachere Implementierung; Filter kann bedarfsgetrieben nachgezogen werden |
| CLI-Name | **`--map skeleton`** | Passt ins bestehende `--map`-Ökosystem; `skeleton` ist in der Branche etabliert |
| Output | **stdout (Markdown)** | Konsistent mit anderen Maps; Agent/User kann in Datei umleiten |
| Datei-Struktur | **`Maps/Skeleton/`** — 4 Klassen | 4 Dateien → Subdirectory rechtfertigt sich; bei MaxDirectoryDepth 4 zulässig |
| Member-Filter | **Alle Member** inkl. private Methoden | LLMs brauchen vollständige Signatur-Sicht; private Methoden nach hinten sortiert |

### Was im `Uses:`-Kommentar erscheint

Ein Methoden-Rumpf wird gescannt auf **direkte Felder-Zugriffe** (`this._field.*` oder `_field.*`).
Nur Felder, deren deklarierter Typ folgendes Kriterium erfüllt, erscheinen im `Uses:`-Kommentar:
- Typ-Name beginnt mit `I` und hat einen Großbuchstaben als zweites Zeichen (Interface-Konvention), **oder**
- Typ-Name endet auf: `Repository`, `Service`, `Handler`, `Client`, `Gateway`, `Manager`,
  `Sender`, `Factory`, `Provider`, `Logger`, `Writer`, `Reader`

Das filtert BCL-Typen (`IDisposable`, `IEnumerable`, `string`, etc.) heraus und fokussiert
auf Domänen-Abhängigkeiten.

---

## 2. CLI-Interface

```
ainetlinter --map skeleton --path ./src/MySolution.sln
ainetlinter --map skeleton --path ./src/          # Sucht .sln/.slnx im Verzeichnis
```

Exit-Code: 0 (Erfolg), 1 (Fehler — Pfad nicht gefunden, kein .sln).

---

## 3. Ausgabe-Format (vollständiges Beispiel)

```markdown
# AiNetLinter — Skeleton Map

> Erzeugt: 2026-06-26 13:47 | Typen: 3 | Member: 12 | Pfad: src/MySolution.sln

---

## AiNetLinter.Features.Orders

### CreateOrderHandler : IHandler<CreateOrderCommand>, IDisposable `sealed`
`src/AiNetLinter/Features/Orders/CreateOrderHandler.cs`

```csharp
private readonly IOrderRepository _orderRepo;
private readonly IPaymentService _payment;
private readonly ILogger<CreateOrderHandler> _logger;

public CreateOrderHandler(IOrderRepository orderRepo, IPaymentService payment, ILogger<CreateOrderHandler> logger)

public async Task<Result<Order>> HandleAsync(CreateOrderCommand cmd, CancellationToken ct) // Throws: ArgumentNullException | Uses: IOrderRepository, IPaymentService
private void Validate(CreateOrderCommand cmd) // Throws: ArgumentNullException
public void Dispose()
```

---

### OrderDto `record`
`src/AiNetLinter/Features/Orders/OrderDto.cs`

```csharp
public Guid Id { get; init; }
public string CustomerName { get; init; }
public decimal TotalAmount { get; init; }
```

---

## AiNetLinter.Infrastructure

### SqlOrderRepository : IOrderRepository `sealed`
`src/AiNetLinter/Infrastructure/SqlOrderRepository.cs`

```csharp
private readonly DbContext _db;

public SqlOrderRepository(DbContext db)

public async Task<Order?> FindAsync(Guid id, CancellationToken ct) // Uses: DbContext
public async Task SaveAsync(Order order, CancellationToken ct) // Uses: DbContext
```
```

**Sortierung innerhalb eines Typs:**
1. Private `readonly` Felder (Dependency-Deklarationen — wichtigste Kopplung-Info)
2. Konstruktoren
3. Öffentliche Properties (komprimiert — nur `{ get; }` / `{ get; init; }` / etc.)
4. Öffentliche Methoden
5. Interne Methoden
6. Private Methoden

**Sortierung zwischen Typen:** Nach Namespace, dann alphabetisch nach Klassenname.

---

## 4. Neue Dateien

### 4.1 `src/AiNetLinter/Maps/Skeleton/SkeletonTypeInfo.cs`

Reine Daten-Records, kein Verhalten.

```csharp
#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Maps.Skeleton;

internal sealed record SkeletonTypeInfo(
    string Namespace,
    string TypeKind,        // "class" | "record" | "interface" | "enum" | "struct"
    string Modifiers,       // z.B. "public sealed" | "internal static"
    string Name,            // inkl. Typparameter: "Handler<TCmd>"
    string? BaseTypes,      // ": IHandler<TCmd>, IDisposable" oder null
    string RelativePath,
    IReadOnlyList<SkeletonMemberInfo> Members
);

internal sealed record SkeletonMemberInfo(
    MemberKind Kind,
    string Signature,       // normalisierte Signatur, einzeilig
    string? MetaComment     // "Throws: X | Uses: IRepo" oder null
);

internal enum MemberKind
{
    Field,
    Constructor,
    Property,
    PublicMethod,
    InternalMethod,
    PrivateMethod,
    Event,
}
```

---

### 4.2 `src/AiNetLinter/Maps/Skeleton/SkeletonSyntaxWalker.cs`

Kernlogik. Wandert den Syntax-Baum eines Dokuments ab und baut `SkeletonTypeInfo`-Liste.

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Maps.Skeleton;

/// <summary>
/// Extrahiert Typ-Skelette (Signaturen + Metadaten) aus einem C#-Syntaxbaum via SemanticModel.
/// </summary>
internal sealed class SkeletonSyntaxWalker : CSharpSyntaxWalker
{
    private static readonly IReadOnlySet<string> DependencySuffixes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Repository", "Service", "Handler", "Client", "Gateway",
        "Manager", "Sender", "Factory", "Provider", "Logger", "Writer", "Reader",
    };

    private readonly SemanticModel _semanticModel;
    private readonly string _relativePath;
    private readonly List<SkeletonTypeInfo> _types = [];
    private string _currentNamespace = "";

    public IReadOnlyList<SkeletonTypeInfo> Types => _types;

    internal SkeletonSyntaxWalker(SemanticModel semanticModel, string relativePath)
        : base(SyntaxWalkerDepth.Node)
    {
        _semanticModel = semanticModel;
        _relativePath = relativePath;
    }

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        var previous = _currentNamespace;
        _currentNamespace = node.Name.ToString();
        base.VisitNamespaceDeclaration(node);
        _currentNamespace = previous;
    }

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        _currentNamespace = node.Name.ToString();
        base.VisitFileScopedNamespaceDeclaration(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        if (IsNestedType(node)) return;
        _types.Add(BuildTypeInfo("class", node.Modifiers, node.Identifier.Text,
            node.TypeParameterList, node.BaseList, node.Members));
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        if (IsNestedType(node)) return;
        var kind = node.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record";
        _types.Add(BuildTypeInfo(kind, node.Modifiers, node.Identifier.Text,
            node.TypeParameterList, node.BaseList, node.Members));
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        if (IsNestedType(node)) return;
        _types.Add(BuildTypeInfo("interface", node.Modifiers, node.Identifier.Text,
            node.TypeParameterList, node.BaseList, node.Members));
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (IsNestedType(node)) return;
        _types.Add(BuildTypeInfo("struct", node.Modifiers, node.Identifier.Text,
            node.TypeParameterList, node.BaseList, node.Members));
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        if (IsNestedType(node)) return;
        var members = node.Members
            .Select(m => new SkeletonMemberInfo(MemberKind.Field, m.Identifier.Text, null))
            .ToList();
        _types.Add(new SkeletonTypeInfo(
            _currentNamespace,
            "enum",
            BuildModifiers(node.Modifiers),
            node.Identifier.Text,
            null,
            _relativePath,
            members));
    }

    // ── Private Helpers ──────────────────────────────────────────────────────

    private static bool IsNestedType(SyntaxNode node) =>
        node.Parent is TypeDeclarationSyntax;

    private SkeletonTypeInfo BuildTypeInfo(
        string typeKind,
        SyntaxTokenList modifiers,
        string name,
        TypeParameterListSyntax? typeParams,
        BaseListSyntax? baseList,
        SyntaxList<MemberDeclarationSyntax> members)
    {
        var fullName = name + (typeParams?.ToString() ?? "");
        var baseTypes = baseList != null ? ": " + baseList.Types.ToString() : null;
        var memberInfos = ExtractMembers(members);

        return new SkeletonTypeInfo(
            _currentNamespace,
            typeKind,
            BuildModifiers(modifiers),
            fullName,
            baseTypes,
            _relativePath,
            memberInfos);
    }

    private List<SkeletonMemberInfo> ExtractMembers(SyntaxList<MemberDeclarationSyntax> members)
    {
        var result = new List<SkeletonMemberInfo>();

        foreach (var member in members)
        {
            var info = member switch
            {
                FieldDeclarationSyntax f       => BuildFieldInfo(f),
                ConstructorDeclarationSyntax c => BuildConstructorInfo(c),
                PropertyDeclarationSyntax p    => BuildPropertyInfo(p),
                MethodDeclarationSyntax m      => BuildMethodInfo(m),
                EventFieldDeclarationSyntax e  => BuildEventInfo(e),
                _                              => null,
            };

            if (info != null) result.Add(info);
        }

        return result;
    }

    private static SkeletonMemberInfo BuildFieldInfo(FieldDeclarationSyntax node)
    {
        var sig = node.ToString().Trim().TrimEnd(';') + ";";
        sig = NormalizeWhitespace(sig);
        return new SkeletonMemberInfo(MemberKind.Field, sig, null);
    }

    private static SkeletonMemberInfo BuildPropertyInfo(PropertyDeclarationSyntax node)
    {
        var accessors = node.AccessorList != null
            ? "{ " + string.Join(" ", node.AccessorList.Accessors.Select(a => a.Keyword.Text + ";")) + " }"
            : "=> /* computed */";
        var sig = $"{BuildModifiers(node.Modifiers)} {node.Type} {node.Identifier.Text} {accessors}";
        return new SkeletonMemberInfo(MemberKind.Property, NormalizeWhitespace(sig), null);
    }

    private SkeletonMemberInfo BuildConstructorInfo(ConstructorDeclarationSyntax node)
    {
        var paramList = FormatParameters(node.ParameterList);
        var sig = $"{BuildModifiers(node.Modifiers)} {node.Identifier.Text}({paramList})";
        var meta = ExtractMethodMeta(node.Body, node.ExpressionBody, node);
        return new SkeletonMemberInfo(MemberKind.Constructor, NormalizeWhitespace(sig), meta);
    }

    private SkeletonMemberInfo BuildMethodInfo(MethodDeclarationSyntax node)
    {
        var typeParams = node.TypeParameterList?.ToString() ?? "";
        var paramList = FormatParameters(node.ParameterList);
        var sig = $"{BuildModifiers(node.Modifiers)} {node.ReturnType} {node.Identifier.Text}{typeParams}({paramList})";
        var meta = ExtractMethodMeta(node.Body, node.ExpressionBody, node);
        var kind = ClassifyMethodKind(node.Modifiers);
        return new SkeletonMemberInfo(kind, NormalizeWhitespace(sig), meta);
    }

    private static SkeletonMemberInfo BuildEventInfo(EventFieldDeclarationSyntax node)
    {
        var sig = NormalizeWhitespace(node.ToString().Trim());
        return new SkeletonMemberInfo(MemberKind.Event, sig, null);
    }

    private string? ExtractMethodMeta(
        BlockSyntax? body,
        ArrowExpressionClauseSyntax? exprBody,
        SyntaxNode context)
    {
        SyntaxNode? bodyNode = body ?? (SyntaxNode?)exprBody;
        if (bodyNode == null) return null;

        var throws = ExtractThrowTypes(bodyNode);
        var uses = ExtractUsedDependencyTypes(bodyNode, context);

        var parts = new List<string>();
        if (throws.Count > 0) parts.Add("Throws: " + string.Join(", ", throws));
        if (uses.Count > 0)   parts.Add("Uses: " + string.Join(", ", uses));

        return parts.Count > 0 ? string.Join(" | ", parts) : null;
    }

    private IReadOnlyList<string> ExtractThrowTypes(SyntaxNode body)
    {
        var types = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var node in body.DescendantNodes())
        {
            ObjectCreationExpressionSyntax? creation = node switch
            {
                ThrowStatementSyntax ts  => ts.Expression as ObjectCreationExpressionSyntax,
                ThrowExpressionSyntax te => te.Expression as ObjectCreationExpressionSyntax,
                _                        => null,
            };

            if (creation == null) continue;

            var typeInfo = _semanticModel.GetTypeInfo(creation);
            var typeName = typeInfo.Type?.Name;
            if (!string.IsNullOrEmpty(typeName))
                types.Add(typeName);
        }

        return [.. types];
    }

    private IReadOnlyList<string> ExtractUsedDependencyTypes(SyntaxNode body, SyntaxNode context)
    {
        var containingType = _semanticModel.GetDeclaredSymbol(context)?.ContainingType;
        if (containingType == null) return [];

        var typeNames = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var identifier in body.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var symbol = _semanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol is not (IFieldSymbol or IPropertySymbol)) continue;
            if (!SymbolEqualityComparer.Default.Equals(symbol.ContainingType, containingType)) continue;

            var typeName = symbol switch
            {
                IFieldSymbol f    => f.Type.Name,
                IPropertySymbol p => p.Type.Name,
                _                 => null,
            };

            if (!string.IsNullOrEmpty(typeName) && IsDependencyType(typeName))
                typeNames.Add(typeName);
        }

        return [.. typeNames];
    }

    private static bool IsDependencyType(string typeName)
    {
        if (typeName.Length >= 2 && typeName[0] == 'I' && char.IsUpper(typeName[1]))
            return true;

        foreach (var suffix in DependencySuffixes)
        {
            if (typeName.EndsWith(suffix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static MemberKind ClassifyMethodKind(SyntaxTokenList modifiers)
    {
        foreach (var mod in modifiers)
        {
            if (mod.IsKind(SyntaxKind.PublicKeyword))    return MemberKind.PublicMethod;
            if (mod.IsKind(SyntaxKind.InternalKeyword))  return MemberKind.InternalMethod;
        }
        return MemberKind.PrivateMethod;
    }

    private static string BuildModifiers(SyntaxTokenList modifiers)
    {
        var text = modifiers.ToString().Trim();
        return string.IsNullOrEmpty(text) ? "private" : text;
    }

    private static string FormatParameters(ParameterListSyntax paramList) =>
        string.Join(", ", paramList.Parameters.Select(p => NormalizeWhitespace(p.ToString())));

    private static string NormalizeWhitespace(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\s+", " ");
}
```

> **Hinweis:** Die Methode `ExtractUsedDependencyTypes` erhält `context` (den Methoden-/Konstruktor-Knoten),
> um `GetDeclaredSymbol` aufzurufen und damit `containingType` zu bestimmen. Das ist nötig, weil
> `ConstructorDeclarationSyntax` und `MethodDeclarationSyntax` in einer gemeinsamen privaten Methode
> behandelt werden, aber beide `SyntaxNode` sind.
>
> **Achtung:** Der Walker überschreibt `VisitClassDeclaration` etc. und ruft `base.Visit*` **nicht** auf,
> um verschachtelte Typen zu ignorieren (`IsNestedType`-Check). Daher wird `BuildTypeInfo` direkt
> aufgerufen, ohne Rekursion in die Kinder-Typen.

---

### 4.3 `src/AiNetLinter/Maps/Skeleton/SkeletonMarkdownRenderer.cs`

Reiner Formatter ohne Roslyn-Abhängigkeit.

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AiNetLinter.Maps.Skeleton;

/// <summary>
/// Rendert eine Liste von <see cref="SkeletonTypeInfo"/>-Objekten als Markdown.
/// </summary>
internal static class SkeletonMarkdownRenderer
{
    internal static string Render(
        IReadOnlyList<SkeletonTypeInfo> types,
        string solutionPath,
        DateTimeOffset generatedAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AiNetLinter — Skeleton Map");
        sb.AppendLine();
        sb.AppendLine($"> Erzeugt: {generatedAt:yyyy-MM-dd HH:mm}"
            + $" | Typen: {types.Count}"
            + $" | Member: {types.Sum(t => t.Members.Count)}"
            + $" | Pfad: {solutionPath.Replace('\\', '/')}");

        var byNamespace = types
            .GroupBy(t => t.Namespace)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var ns in byNamespace)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"## {ns.Key}");

            foreach (var type in ns.OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                AppendType(sb, type);
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendType(StringBuilder sb, SkeletonTypeInfo type)
    {
        sb.AppendLine();
        var modifierTag = BuildModifierTag(type.Modifiers);
        var basePart = type.BaseTypes != null ? $" {type.BaseTypes}" : "";
        sb.AppendLine($"### {type.Name}{basePart}{modifierTag}");
        sb.AppendLine($"`{type.RelativePath}`");
        sb.AppendLine();
        sb.AppendLine("```csharp");

        AppendMembersOfKind(sb, type.Members, MemberKind.Field);
        AppendMembersOfKind(sb, type.Members, MemberKind.Constructor, addBlankBefore: true);
        AppendMembersOfKind(sb, type.Members, MemberKind.Property, addBlankBefore: true);
        AppendMembersOfKind(sb, type.Members, MemberKind.PublicMethod, addBlankBefore: true);
        AppendMembersOfKind(sb, type.Members, MemberKind.InternalMethod, addBlankBefore: true);
        AppendMembersOfKind(sb, type.Members, MemberKind.Event, addBlankBefore: true);
        AppendMembersOfKind(sb, type.Members, MemberKind.PrivateMethod, addBlankBefore: true);

        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("---");
    }

    private static void AppendMembersOfKind(
        StringBuilder sb,
        IReadOnlyList<SkeletonMemberInfo> members,
        MemberKind kind,
        bool addBlankBefore = false)
    {
        var filtered = members.Where(m => m.Kind == kind).ToList();
        if (filtered.Count == 0) return;

        if (addBlankBefore && sb.Length > 0 && sb[^1] != '\n')
            sb.AppendLine();
        else if (addBlankBefore)
            sb.AppendLine();

        foreach (var m in filtered)
        {
            var line = m.MetaComment != null
                ? $"{m.Signature} // {m.MetaComment}"
                : m.Signature;
            sb.AppendLine(line);
        }
    }

    private static string BuildModifierTag(string modifiers)
    {
        if (modifiers.Contains("sealed"))   return " `sealed`";
        if (modifiers.Contains("abstract")) return " `abstract`";
        if (modifiers.Contains("static"))   return " `static`";
        return "";
    }
}
```

---

### 4.4 `src/AiNetLinter/Maps/Skeleton/SkeletonMapBuilder.cs`

Orchestriert Workspace-Load, Walker-Ausführung und Rendering.

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using AiNetLinter.Baseline;
using AiNetLinter.Output;

namespace AiNetLinter.Maps.Skeleton;

/// <summary>
/// Lädt eine Solution via MSBuildWorkspace und erzeugt eine Skeleton Map für LLM-Audits.
/// </summary>
internal static class SkeletonMapBuilder
{
    internal static async Task<int> BuildAsync(
        string targetPath,
        ILintConsole console,
        CancellationToken ct = default)
    {
        using SourceFileCatalog catalog = await SourceFileCatalog.LoadAsync(targetPath, ct);
        var solutionPath = catalog.Solution.FilePath ?? targetPath;
        var solutionDir = Path.GetDirectoryName(solutionPath) ?? targetPath;

        var types = await ExtractTypesAsync(catalog.Solution, solutionDir, ct);

        var markdown = SkeletonMarkdownRenderer.Render(types, solutionPath, DateTimeOffset.Now);
        console.WriteLine(markdown);
        return 0;
    }

    private static async Task<IReadOnlyList<SkeletonTypeInfo>> ExtractTypesAsync(
        Solution solution,
        string solutionDir,
        CancellationToken ct)
    {
        var allTypes = new System.Collections.Concurrent.ConcurrentBag<SkeletonTypeInfo>();
        var documents = CollectDocuments(solution, solutionDir);

        await Parallel.ForEachAsync(documents, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = ct,
        }, async (doc, token) =>
        {
            var docTypes = await ExtractFromDocumentAsync(doc, solutionDir, token);
            foreach (var t in docTypes)
                allTypes.Add(t);
        });

        return [.. allTypes];
    }

    private static IReadOnlyList<Document> CollectDocuments(Solution solution, string solutionDir)
    {
        return solution.Projects
            .SelectMany(p => p.Documents)
            .Where(d => SourceFileCatalog.IsValidDocument(d, solutionDir))
            .ToList();
    }

    private static async Task<IReadOnlyList<SkeletonTypeInfo>> ExtractFromDocumentAsync(
        Document document,
        string solutionDir,
        CancellationToken ct)
    {
        var semanticModel = await document.GetSemanticModelAsync(ct);
        if (semanticModel == null) return [];

        var relativePath = PathNormalizer.ToRelative(solutionDir, document.FilePath ?? document.Name);
        var walker = new SkeletonSyntaxWalker(semanticModel, relativePath);
        walker.Visit(semanticModel.SyntaxTree.GetRoot(ct));
        return walker.Types;
    }
}
```

---

## 5. Zu ändernde Dateien

### 5.1 `src/AiNetLinter/Commands/MapCommand.cs`

Die `skeleton`-Branch muss **async** sein — separat vor dem synchronen switch.

**Änderung:**

```csharp
// Bestehender synchroner switch bleibt unverändert.
// VOR der switch-Anweisung einfügen:

if (mapType == "skeleton")
    return await AiNetLinter.Maps.Skeleton.SkeletonMapBuilder.BuildAsync(args.TargetPath, c, ct);

var exitCode = mapType switch
{
    "vocabulary" => VocabularyMapBuilder.Build(args.TargetPath, c),
    "structure"  => StructureMapBuilder.Build(args.TargetPath, ResolveMaxLineCount(args), c),
    "hotspots"   => HotspotMapBuilder.Build(args.TargetPath, ResolveMaxLineCount(args), c),
    _ => ReportUnknownType(mapType, c)
};

return exitCode;   // Task.FromResult() entfällt — return exitCode reicht bei async-Methode
```

> **Achtung:** `MapCommand.RunAsync` ist bereits `async Task<int>`. Das `Task.FromResult(exitCode)` am
> Ende kann direkt zu `return exitCode` werden (weil der `await`-Aufruf oben die Methode bereits async macht).

Die Methode `ReportUnknownType` muss auch `skeleton` in den Hint aufnehmen:

```csharp
hint: "Gültige Typen: vocabulary, structure, hotspots, skeleton"
```

### 5.2 `src/AiNetLinter/Cli/CliOptionFactory.cs`

```csharp
// Vorher:
internal static Option<string?> CreateMapOption() => new("--map")
{
    Description = "Codebase-Landkarte generieren. Erfordert --path. Typen: vocabulary | structure | hotspots",
};

// Nachher:
internal static Option<string?> CreateMapOption() => new("--map")
{
    Description = "Codebase-Landkarte generieren. Erfordert --path. Typen: vocabulary | structure | hotspots | skeleton",
};
```

### 5.3 `src/AiNetLinter/Cli/LinterArgs.cs`

`Validate()` enthält keinen direkten Hinweis auf Map-Typen, aber `MapType` ist bereits in der
Existenzprüfung berücksichtigt — **keine Änderung nötig**.

---

## 6. Unit-Tests

### 6.1 `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonSyntaxWalkerTests.cs`

Tests ohne MSBuildWorkspace — `CSharpSyntaxTree.ParseText` + `CSharpCompilation` für das SemanticModel.

```csharp
#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using AiNetLinter.Maps.Skeleton;

namespace AiNetLinter.Tests.Maps.Skeleton;

public sealed class SkeletonSyntaxWalkerTests
{
    private static (SkeletonSyntaxWalker Walker, SemanticModel Model) CreateWalker(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);
        var walker = new SkeletonSyntaxWalker(model, "Test.cs");
        walker.Visit(tree.GetRoot());
        return (walker, model);
    }

    [Fact]
    public void ExtractsTopLevelClass()
    {
        var code = """
            namespace Foo;
            public sealed class MyService { }
            """;
        var (walker, _) = CreateWalker(code);
        var type = Assert.Single(walker.Types);
        Assert.Equal("class", type.TypeKind);
        Assert.Equal("MyService", type.Name);
        Assert.Equal("Foo", type.Namespace);
    }

    [Fact]
    public void IgnoresNestedTypes()
    {
        var code = """
            namespace Foo;
            public class Outer { private class Inner { } }
            """;
        var (walker, _) = CreateWalker(code);
        var type = Assert.Single(walker.Types);
        Assert.Equal("Outer", type.Name);
    }

    [Fact]
    public void ExtractsThrowsInMethod()
    {
        var code = """
            namespace Foo;
            public class Svc
            {
                public void Run(string s)
                {
                    if (s == null) throw new System.ArgumentNullException(nameof(s));
                }
            }
            """;
        var (walker, _) = CreateWalker(code);
        var method = walker.Types[0].Members
            .First(m => m.Kind == MemberKind.PublicMethod);
        Assert.Contains("Throws: ArgumentNullException", method.MetaComment);
    }

    [Fact]
    public void ExtractsFieldRecord()
    {
        var code = """
            namespace Foo;
            public sealed record MyDto(string Name, int Age);
            """;
        var (walker, _) = CreateWalker(code);
        Assert.Single(walker.Types);
        Assert.Equal("record", walker.Types[0].TypeKind);
    }

    [Fact]
    public void ExtractsInterfaceType()
    {
        var code = """
            namespace Foo;
            public interface IMyContract { void Do(); }
            """;
        var (walker, _) = CreateWalker(code);
        var type = Assert.Single(walker.Types);
        Assert.Equal("interface", type.TypeKind);
    }

    [Fact]
    public void ExtractsEnumMembers()
    {
        var code = """
            namespace Foo;
            public enum Status { Active, Inactive }
            """;
        var (walker, _) = CreateWalker(code);
        var type = Assert.Single(walker.Types);
        Assert.Equal("enum", type.TypeKind);
        Assert.Equal(2, type.Members.Count);
    }

    [Fact]
    public void MethodWithoutBodyHasNullMetaComment()
    {
        var code = """
            namespace Foo;
            public interface IFoo { void Bar(); }
            """;
        var (walker, _) = CreateWalker(code);
        var member = walker.Types[0].Members.First(m => m.Kind == MemberKind.PublicMethod);
        Assert.Null(member.MetaComment);
    }

    [Fact]
    public void ClassifiesMethodAccessibility()
    {
        var code = """
            namespace Foo;
            public class Svc
            {
                public void Pub() { }
                internal void Int() { }
                private void Priv() { }
            }
            """;
        var (walker, _) = CreateWalker(code);
        var members = walker.Types[0].Members;
        Assert.Equal(MemberKind.PublicMethod,   members[0].Kind);
        Assert.Equal(MemberKind.InternalMethod, members[1].Kind);
        Assert.Equal(MemberKind.PrivateMethod,  members[2].Kind);
    }
}
```

### 6.2 `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonMapBuilderTests.cs`

Integrations-Tests via MSBuildWorkspace (analog zu `PlaybookCheckCommandTests` mit `FindSlnxFile`).

```csharp
#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using AiNetLinter.Maps.Skeleton;
using AiNetLinter.Tests.Output;

namespace AiNetLinter.Tests.Maps.Skeleton;

[Collection("ConsoleTestCollection")]
public sealed class SkeletonMapBuilderTests
{
    [Fact]
    public async Task BuildAsync_WithSolution_ReturnsZeroAndContainsMarkdown()
    {
        var slnPath = FindSlnxFile();
        if (slnPath == null) return; // kein .slnx im CI — überspringen

        var console = new TestLintConsole();
        var result = await SkeletonMapBuilder.BuildAsync(slnPath, console);

        Assert.Equal(0, result);
        var output = console.Output;
        Assert.Contains("# AiNetLinter — Skeleton Map", output);
        Assert.Contains("```csharp", output);
    }

    [Fact]
    public async Task BuildAsync_InvalidPath_ReturnsOne()
    {
        var console = new TestLintConsole();
        // SourceFileCatalog.LoadAsync wirft FileNotFoundException
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => SkeletonMapBuilder.BuildAsync("/nonexistent/path", console));
    }

    private static string? FindSlnxFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var files = dir.GetFiles("*.slnx");
            if (files.Length > 0) return files[0].FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
```

> **Hinweis:** Der `SkeletonMapBuilder`-Integrationstest ist langsam (MSBuildWorkspace-Load).
> Er ist korrekt in `[Collection("ConsoleTestCollection")]` um Console-Konflikte zu vermeiden.
> Das Überspringen bei nicht-gefundenem `.slnx` ist bewusste CI-Robustheit (kein `Skip`-Attribut nötig).

---

## 7. Dokumentations-Updates

### 7.1 `Docs/ROADMAP.md`

Neue Zeile im "Geplant"-Abschnitt oder als erledigtes Feature eintragen:

```markdown
| `--map skeleton` | Semantisches Code-Skelett für LLM-Audits (Signaturen + Throws + Uses) | ✅ |
```

### 7.2 `Docs/configuration.md`

Neuer Abschnitt unter `--map`:

```markdown
#### `--map skeleton`

Erzeugt eine vollständige **Skeleton Map** der Solution: Für jeden Typ werden Namespace,
Modifikatoren, Basistypen und alle Member-Signaturen ausgegeben. Methoden-Rümpfe
werden durch Inline-Kommentare ersetzt:

- `// Throws: X` — geworfene Exception-Typen (aus `throw new X()`-Statements)
- `// Uses: IRepo, IService` — injizierte Abhängigkeiten, auf die die Methode zugreift

**Erfordert:** `--path` zu einer `.sln`- oder `.slnx`-Datei (oder Verzeichnis mit einer davon).
**Ausgabe:** stdout (Markdown). Empfehlung: in Datei umleiten und als Kontext für LLM-Audit nutzen.

**Token-Ersparnis:** ~70–85% gegenüber rohem Quellcode bei vollem Erhalt der Architektur-Information.

**Anwendungsfälle:** Code-Duplikat-Erkennung, Naming-Drift-Audit, Abhängigkeitsanalyse,
Architektur-Review durch LLM-Agenten.

```bash
ainetlinter --map skeleton --path ./src/MySolution.sln > skeleton.md
```
```

### 7.3 `README.md`

Im Map-Abschnitt die Aufzählung um `skeleton` ergänzen:

```markdown
| `--map vocabulary` | Typ-Gruppen nach Suffix — Naming-Drift-Eingabe |
| `--map structure`  | Verzeichnis- und Dateigrößen-Übersicht |
| `--map hotspots`   | Dateien nahe am Limit — proaktive Warnung |
| `--map skeleton`   | Komprimiertes Code-Skelett (Signaturen + Metadaten) für LLM-Audits |
```

---

## 8. Bekannte Einschränkungen (v1)

| Einschränkung | Ursache | Workaround / v2 |
|:---|:---|:---|
| Kein Namespace-Filter | v1-Scope | `--namespace Foo.Bar` in v2 |
| Langsamer Start (10–30s) | MSBuildWorkspace | Kein Cache für Skeleton-Pass |
| Test-Dateien werden ausgegeben | Kein Test-Filter | `--exclude-tests` in v2 oder `_` für Test-Assemblies |
| Verschachtelte Typen fehlen | v1-Scope (IsNestedType-Skip) | v2 kann nested types optional einbeziehen |

---

## 9. Implementierungs-Reihenfolge

```
1. SkeletonTypeInfo.cs         ← Datenmodell zuerst (keine Abhängigkeiten)
2. SkeletonSyntaxWalker.cs     ← Kern-Logik; mit ParseText-Tests validieren
3. SkeletonSyntaxWalkerTests.cs← Gleichzeitig mit Walker entwickeln (TDD)
4. SkeletonMarkdownRenderer.cs ← Formatter; rein unit-testbar ohne Roslyn
5. SkeletonMapBuilder.cs       ← Integration; erst wenn Walker + Renderer stehen
6. MapCommand.cs               ← Anbindung ans CLI
7. CliOptionFactory.cs         ← Beschreibungs-Update
8. SkeletonMapBuilderTests.cs  ← Integrationstest als abschließende Verifikation
9. Docs + README               ← Nach grünem Build
```

---

## 10. Commit-Vorschlag

```
feat: implementiere --map skeleton fuer LLM-optimierte Codebasis-Skelettierung

Neuer Map-Typ, der via MSBuildWorkspace eine Solution semantisch skelettiert:
- Typ-Signaturen (class/record/interface/enum/struct) mit Basistypen und Modifikatoren
- Member-Signaturen einzeilig normalisiert (Felder, Properties, Methoden, Events)
- Methoden-Metadaten: // Throws: X | Uses: IRepo (via SemanticModel aufgeloest)
- Ausgabe als Markdown auf stdout (70-85% Token-Reduktion gegenueber Vollcode)
```
