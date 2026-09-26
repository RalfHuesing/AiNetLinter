#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

internal enum RazorGeneratedEvidenceStatus
{
    NotComponent,
    Available,
    Unavailable,
}

internal sealed class RazorGeneratedEvidenceIndex
{
    internal static readonly RazorGeneratedEvidenceIndex Empty = new(
        new Dictionary<ComponentKey, RazorGeneratedEvidenceStatus>(),
        new Dictionary<DocumentId, Dictionary<string, RazorGeneratedEvidenceStatus>>());

    private static readonly SymbolDisplayFormat TypeIdentityFormat = SymbolDisplayFormat.FullyQualifiedFormat;
    private readonly Dictionary<ComponentKey, RazorGeneratedEvidenceStatus> statuses;
    private readonly Dictionary<DocumentId, Dictionary<string, RazorGeneratedEvidenceStatus>> statusesByDocument;

    private RazorGeneratedEvidenceIndex(
        Dictionary<ComponentKey, RazorGeneratedEvidenceStatus> statuses,
        Dictionary<DocumentId, Dictionary<string, RazorGeneratedEvidenceStatus>> statusesByDocument)
    {
        this.statuses = statuses;
        this.statusesByDocument = statusesByDocument;
    }

    internal static async Task<RazorGeneratedEvidenceIndex> CreateAsync(Project project, CancellationToken cancellationToken)
    {
        var generatedDocuments = await project.GetSourceGeneratedDocumentsAsync(cancellationToken);
        return await CreateAsync(project, generatedDocuments, cancellationToken);
    }

    internal static async Task<RazorGeneratedEvidenceIndex> CreateAsync(
        Project project,
        IEnumerable<Document> generatedDocuments,
        CancellationToken cancellationToken)
    {
        var collector = new CodeBehindCandidateCollector(project);
        await collector.CollectAsync(cancellationToken);
        var statuses = collector.Statuses;
        var statusesByDocument = collector.StatusesByDocument;
        var candidates = collector.Candidates;
        if (candidates.Count == 0) return new(statuses, statusesByDocument);

        var projectDirectory = GetProjectDirectory(project);
        foreach (var generatedDocument in generatedDocuments)
        {
            if (cancellationToken.IsCancellationRequested) break;
            await IndexGeneratedDocumentAsync(
                generatedDocument,
                projectDirectory,
                candidates,
                statuses,
                statusesByDocument,
                cancellationToken);
        }

        return new(statuses, statusesByDocument);
    }

    internal static bool HasMatchingGeneratedDeclaration(INamedTypeSymbol type, string componentPath, Project project)
    {
        var projectDirectory = GetProjectDirectory(project);
        var expectedPath = NormalizeRelativePath(projectDirectory, componentPath);
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax();
            if (declaration is not TypeDeclarationSyntax typeDeclaration
                || !typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)) continue;
            var generatedPath = GetComponentPath(declaration.SyntaxTree.GetRoot());
            if (generatedPath is not null
                && NormalizeRelativePath(projectDirectory, generatedPath).Equals(expectedPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static async Task IndexGeneratedDocumentAsync(
        Document generatedDocument,
        string projectDirectory,
        IReadOnlyList<CodeBehindCandidate> candidates,
        Dictionary<ComponentKey, RazorGeneratedEvidenceStatus> statuses,
        Dictionary<DocumentId, Dictionary<string, RazorGeneratedEvidenceStatus>> statusesByDocument,
        CancellationToken cancellationToken)
    {
        var generatedRoot = await generatedDocument.GetSyntaxRootAsync(cancellationToken);
        if (generatedRoot is null) return;

        var componentPath = GetComponentPath(generatedRoot);
        if (componentPath is null) return;

        var componentKey = NormalizeRelativePath(projectDirectory, componentPath);
        var matchingPathCandidates = candidates
            .Where(candidate => candidate.ComponentPath.Equals(componentKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matchingPathCandidates.Length == 0) return;

        var semanticModel = await generatedDocument.GetSemanticModelAsync(cancellationToken);
        if (semanticModel is null) return;

        foreach (var declaration in generatedRoot.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword)) continue;
            var generatedType = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
            if (generatedType is not null)
            {
                MarkMatchingCandidates(matchingPathCandidates, generatedType, statuses, statusesByDocument);
            }
        }
    }

    private static void MarkMatchingCandidates(
        IEnumerable<CodeBehindCandidate> candidates,
        INamedTypeSymbol generatedType,
        Dictionary<ComponentKey, RazorGeneratedEvidenceStatus> statuses,
        Dictionary<DocumentId, Dictionary<string, RazorGeneratedEvidenceStatus>> statusesByDocument)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.TypeSymbol is null
                || !SymbolEqualityComparer.Default.Equals(candidate.TypeSymbol, generatedType)) continue;

            SetStatus(statuses, statusesByDocument, candidate.Key, RazorGeneratedEvidenceStatus.Available);
        }
    }

    internal RazorGeneratedEvidenceStatus GetStatus(ISymbol? symbol, Document document)
    {
        var containingType = symbol switch
        {
            INamedTypeSymbol namedType => namedType,
            null => null,
            _ => symbol.ContainingType,
        };

        if (containingType is not null
            && statuses.TryGetValue(new(document.Id, GetTypeIdentity(containingType)), out var status))
        {
            return status;
        }

        return symbol is null
            && statusesByDocument.TryGetValue(document.Id, out var documentStatuses)
            && documentStatuses.Count == 1
                ? documentStatuses.Values.Single()
                : RazorGeneratedEvidenceStatus.NotComponent;
    }

    internal static DeadCodeRazorEvidenceAssessment Assess(
        RazorGeneratedEvidenceStatus status,
        string confidence,
        string reason,
        IReadOnlyList<string> countercheck)
    {
        return status switch
        {
            RazorGeneratedEvidenceStatus.Unavailable => new(
                confidence.Equals("high", StringComparison.OrdinalIgnoreCase) ? "low" : confidence,
                "Razor-Referenzen nicht entscheidbar: generiertes C# fehlt oder ist nicht auswertbar; Razor-Generierung/Projektladung gegenprüfen.",
                countercheck.Concat(["Razor-Generierung/Projektladung"]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()),
            RazorGeneratedEvidenceStatus.Available => new(
                confidence,
                $"{reason} Generierte Razor-Referenzen wurden mitgeprüft.",
                countercheck),
            _ => new(confidence, reason, countercheck),
        };
    }

    internal static DeadCodeRazorEvidenceAssessment AssessMember(
        RazorGeneratedEvidenceStatus status,
        string confidence) => Assess(
            status,
            confidence,
            confidence == "high"
                ? "Keine Referenzen innerhalb der Solution gefunden (privates/internes Element ohne Framework-Marker)."
                : "Keine Referenzen in der Solution gefunden (oeffentliche API oder moegliche Framework-Bindung).",
            ["Reflection", "DI", "Generatoren", "dynamic", "externe Consumer"]);

    private sealed class CodeBehindCandidateCollector(Project project)
    {
        public Dictionary<ComponentKey, RazorGeneratedEvidenceStatus> Statuses { get; } = [];
        public Dictionary<DocumentId, Dictionary<string, RazorGeneratedEvidenceStatus>> StatusesByDocument { get; } = [];
        public List<CodeBehindCandidate> Candidates { get; } = [];

        public async Task CollectAsync(CancellationToken cancellationToken)
        {
            foreach (var document in project.Documents)
            {
                if (cancellationToken.IsCancellationRequested) break;
                await CollectDocumentAsync(document, cancellationToken);
            }
        }

        private async Task CollectDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            if (document.FilePath is not { } codeBehindPath
                || !codeBehindPath.EndsWith(".razor.cs", StringComparison.OrdinalIgnoreCase)) return;

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            if (syntaxRoot is null) return;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var componentPath = Path.GetFullPath(codeBehindPath[..^".cs".Length]);
            var componentName = Path.GetFileNameWithoutExtension(componentPath);
            var componentKey = NormalizeRelativePath(GetProjectDirectory(project), componentPath);
            var hasComponentFile = File.Exists(componentPath);
            var fileInfo = new CodeBehindFileInfo(componentName, componentKey, hasComponentFile);

            foreach (var declaration in syntaxRoot.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                AddCandidate(document, declaration, fileInfo, semanticModel, cancellationToken);
            }
        }

        private void AddCandidate(
            Document document,
            TypeDeclarationSyntax declaration,
            CodeBehindFileInfo fileInfo,
            SemanticModel? semanticModel,
            CancellationToken cancellationToken)
        {
            if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                || !declaration.Identifier.ValueText.Equals(fileInfo.ComponentName, StringComparison.Ordinal)) return;

            var typeSymbol = semanticModel?.GetDeclaredSymbol(declaration, cancellationToken);
            var key = new ComponentKey(document.Id, typeSymbol is null
                ? declaration.Identifier.ValueText
                : GetTypeIdentity(typeSymbol));
            var status = fileInfo.HasComponentFile
                ? RazorGeneratedEvidenceStatus.Unavailable
                : RazorGeneratedEvidenceStatus.NotComponent;
            SetStatus(Statuses, StatusesByDocument, key, status);
            if (fileInfo.HasComponentFile)
            {
                Candidates.Add(new(key, fileInfo.ComponentPath, typeSymbol));
            }
        }
    }

    private sealed record CodeBehindFileInfo(
        string ComponentName,
        string ComponentPath,
        bool HasComponentFile);

    private static string? GetComponentPath(SyntaxNode syntaxRoot)
    {
        foreach (var trivia in syntaxRoot.DescendantTrivia(descendIntoTrivia: true))
        {
            if (trivia.GetStructure() is not PragmaChecksumDirectiveTriviaSyntax checksum) continue;
            var filePath = checksum.File.ValueText;
            if (filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)) return filePath;
        }

        return null;
    }

    private static string NormalizeRelativePath(string projectDirectory, string path)
    {
        var absolutePath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(projectDirectory, path));
        return Path.GetRelativePath(projectDirectory, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string GetProjectDirectory(Project project) =>
        Path.GetDirectoryName(project.FilePath)
        ?? Path.GetDirectoryName(project.Solution.FilePath)
        ?? Environment.CurrentDirectory;

    private static string GetTypeIdentity(INamedTypeSymbol typeSymbol) =>
        typeSymbol.OriginalDefinition.ToDisplayString(TypeIdentityFormat);

    private static void SetStatus(
        Dictionary<ComponentKey, RazorGeneratedEvidenceStatus> statuses,
        Dictionary<DocumentId, Dictionary<string, RazorGeneratedEvidenceStatus>> statusesByDocument,
        ComponentKey key,
        RazorGeneratedEvidenceStatus status)
    {
        statuses[key] = status;
        if (!statusesByDocument.TryGetValue(key.DocumentId, out var documentStatuses))
        {
            documentStatuses = new(StringComparer.Ordinal);
            statusesByDocument.Add(key.DocumentId, documentStatuses);
        }

        documentStatuses[key.TypeIdentity] = status;
    }

    private sealed record CodeBehindCandidate(
        ComponentKey Key,
        string ComponentPath,
        INamedTypeSymbol? TypeSymbol);

    private readonly record struct ComponentKey(DocumentId DocumentId, string TypeIdentity);
}

internal sealed record DeadCodeRazorEvidenceAssessment(
    string Confidence,
    string Reason,
    IReadOnlyList<string> Countercheck);
