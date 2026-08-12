#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.TestKit;

/// <summary>
/// Deklarative Projektbeschreibung fuer <see cref="RoslynTestSolutionFactory.CreateSolution"/>.
/// <see cref="ProjectReferences"/> referenziert andere <see cref="Name"/>-Werte desselben
/// <see cref="RoslynTestSolutionFactory.CreateSolution"/>-Aufrufs.
/// </summary>
public sealed record ProjectSpec(
    string Name,
    IReadOnlyList<(string FileName, string Content)> Documents,
    IReadOnlyList<string>? ProjectReferences = null,
    IReadOnlyList<MetadataReference>? AdditionalReferences = null,
    NullableContextOptions Nullable = NullableContextOptions.Enable,
    IReadOnlyList<string>? PreprocessorSymbols = null,
    OutputKind OutputKind = OutputKind.DynamicallyLinkedLibrary);

/// <summary>
/// Immutable Solution-Snapshot plus der Besitzer des zugrunde liegenden <see cref="Workspace"/> zur
/// kontrollierten Entsorgung. Der Workspace ist write-once (siehe <see cref="PreparedSolutionFixture"/>) --
/// Konsumenten mutieren nicht auf der zurueckgegebenen <see cref="Solution"/>, sondern bauen bei Bedarf
/// einen eigenen Snapshot ueber eine eigene <see cref="RoslynTestSolutionFactory.CreateSolution"/>-Instanz.
/// </summary>
public sealed record RoslynTestSolution(Solution Solution, Workspace Workspace) : IDisposable
{
    public void Dispose() => Workspace.Dispose();
}

/// <summary>
/// Zentraler, mehrprojekt-faehiger In-Memory-Solution-Builder auf <see cref="AdhocWorkspace"/>-Basis.
/// Der Kern-Referenzsatz (<see cref="CoreReferences"/>) wird einmalig statisch gebaut und ueber alle
/// Aufrufe hinweg wiederverwendet -- anders als das Legacy-Muster in
/// <c>AiNetLinter.Tests.TestHelper.ParseCode</c>, das den vollen Referenzsatz bei jedem Aufruf neu per
/// <see cref="AppDomain.CurrentDomain"/>-Scan aufbaut.
/// </summary>
public static class RoslynTestSolutionFactory
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> CoreReferencesLazy = new(BuildCoreReferences);

    /// <summary>
/// Einmalig gebauter, testframework-freier BCL-Kern-Referenzsatz. Ueber alle
/// <see cref="CreateSolution"/>-Aufrufe hinweg dieselben <see cref="MetadataReference"/>-Objekte
/// (Referenzgleichheit der einzelnen Eintraege).
    /// </summary>
    public static ImmutableArray<MetadataReference> CoreReferences => CoreReferencesLazy.Value;

    /// <summary>
    /// Baut eine neue <see cref="AdhocWorkspace"/>-Solution aus den gegebenen <paramref name="specs"/>.
    /// Wirft <see cref="InvalidOperationException"/>, wenn ein <see cref="ProjectSpec.ProjectReferences"/>-Eintrag
    /// keinen der uebergebenen Spec-Namen trifft.
    /// </summary>
    public static RoslynTestSolution CreateSolution(params ProjectSpec[] specs)
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var projectIdsByName = new Dictionary<string, ProjectId>(StringComparer.Ordinal);

        foreach (var spec in specs)
        {
            var projectId = ProjectId.CreateNewId(spec.Name);
            projectIdsByName[spec.Name] = projectId;
            solution = AddProject(solution, projectId, spec);
        }

        foreach (var spec in specs)
        {
            solution = WireProjectReferences(solution, spec, projectIdsByName);
        }

        return new RoslynTestSolution(solution, workspace);
    }

    private static Solution WireProjectReferences(
        Solution solution, ProjectSpec spec, IReadOnlyDictionary<string, ProjectId> projectIdsByName)
    {
        if (spec.ProjectReferences is null)
        {
            return solution;
        }

        var projectId = projectIdsByName[spec.Name];
        foreach (var referencedName in spec.ProjectReferences)
        {
            if (!projectIdsByName.TryGetValue(referencedName, out var referencedId))
            {
                throw new InvalidOperationException(
                    $"ProjectSpec '{spec.Name}' referenziert unbekanntes Projekt '{referencedName}'.");
            }

            solution = solution.AddProjectReference(projectId, new ProjectReference(referencedId));
        }

        return solution;
    }

    private static Solution AddProject(Solution solution, ProjectId projectId, ProjectSpec spec)
    {
        var references = spec.AdditionalReferences is { Count: > 0 }
            ? CoreReferences.Concat(spec.AdditionalReferences).ToImmutableArray()
            : CoreReferences;

        var compilationOptions = new CSharpCompilationOptions(
            spec.OutputKind,
            nullableContextOptions: spec.Nullable);

        var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                spec.Name,
                spec.Name,
                LanguageNames.CSharp)
            .WithMetadataReferences(references)
            .WithCompilationOptions(compilationOptions);

        if (spec.PreprocessorSymbols is { Count: > 0 })
        {
            projectInfo = projectInfo.WithParseOptions(new CSharpParseOptions(preprocessorSymbols: spec.PreprocessorSymbols));
        }

        solution = solution.AddProject(projectInfo);

        foreach (var (fileName, content) in spec.Documents)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, fileName, content);
        }

        return solution;
    }

    private static ImmutableArray<MetadataReference> BuildCoreReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(System.Runtime.GCSettings).Assembly,
            typeof(Enumerable).Assembly,
            typeof(System.Threading.Tasks.Task).Assembly,
        };

        return assemblies
            .Select(assembly => assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(location => (MetadataReference)MetadataReference.CreateFromFile(location))
            .ToImmutableArray();
    }
}
