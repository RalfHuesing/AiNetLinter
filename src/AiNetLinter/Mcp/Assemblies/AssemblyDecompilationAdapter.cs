#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class AssemblyDecompilationAdapter
{
    internal Task<DecompilationResult> DecompileAsync(
        DecompilationRequest request,
        AssemblyReferenceResolution references)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken);
        deadline.CancelAfter(request.Options.EffectiveTimeout);
        try
        {
            var selection = SelectTypes(ReadTopLevelTypes(request.AssemblyPath, deadline.Token), request.Options);
            var decompiler = CreateDecompiler(request.AssemblyPath, references, deadline.Token);
            var documents = DecompileTypes(decompiler, selection.Types, request.Options, deadline.Token, selection.Diagnostics);
            AddModuleDocumentIfRequired(decompiler, request.Options, selection, documents, deadline.Token);
            return Task.FromResult(new DecompilationResult(documents, selection.Diagnostics, selection.Diagnostics.Count == 0));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(new DecompilationResult(
                [],
                [new AssemblySessionDiagnostic("assembly-decompilation-cancelled", "Die Decompilation wurde wegen Cancellation oder Deadline abgebrochen.", "error")],
                false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException or ArgumentException or ICSharpCode.Decompiler.DecompilerException)
        {
            return Task.FromResult(new DecompilationResult(
                [],
                [new AssemblySessionDiagnostic("assembly-decompilation-failed", $"Decompilation fehlgeschlagen: {ex.Message}", "error")],
                false));
        }
    }

    private static List<DecompiledDocument> DecompileTypes(
        ICSharpCode.Decompiler.CSharp.CSharpDecompiler decompiler,
        IReadOnlyList<TypeDefinitionInfo> types,
        AssemblyDecompilationOptions options,
        CancellationToken cancellationToken,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        var documents = new List<DecompiledDocument>(types.Count);
        foreach (var type in types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var source = decompiler.DecompileTypesAsString([type.Handle]);
                cancellationToken.ThrowIfCancellationRequested();
                if (source.Length > options.MaxDocumentCharacters)
                {
                    diagnostics.Add(new(
                        "assembly-document-size-limit",
                        $"Der dekompilierte Typ '{type.MetadataName}' überschreitet die Dokumentgrenze."));
                    continue;
                }

                documents.Add(new DecompiledDocument(
                    $"source/{documents.Count:D5}-{Sanitize(type.MetadataName)}.cs",
                    type.MetadataName,
                    source,
                    $"0x{MetadataTokens.GetToken(type.Handle):X8}"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or ICSharpCode.Decompiler.DecompilerException)
            {
                diagnostics.Add(new(
                    "assembly-type-decompilation-failed",
                    $"Typ '{type.MetadataName}' konnte nicht dekompiliert werden: {ex.Message}"));
            }
        }

        return documents;
    }

    private static ICSharpCode.Decompiler.CSharp.CSharpDecompiler CreateDecompiler(
        string assemblyPath,
        AssemblyReferenceResolution references,
        CancellationToken cancellationToken)
    {
        var settings = new ICSharpCode.Decompiler.DecompilerSettings
        {
            DecompileMemberBodies = true,
            ShowXmlDocumentation = false,
            UseDebugSymbols = false,
        };
        return new ICSharpCode.Decompiler.CSharp.CSharpDecompiler(assemblyPath, references.DecompilerResolver, settings)
        {
            CancellationToken = cancellationToken,
        };
    }

    private static string DecompileModule(
        ICSharpCode.Decompiler.CSharp.CSharpDecompiler decompiler,
        AssemblyDecompilationOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = decompiler.DecompileWholeModuleAsString();
        cancellationToken.ThrowIfCancellationRequested();
        return source.Length <= options.MaxDocumentCharacters ? source : string.Empty;
    }

    private static IReadOnlyList<TypeDefinitionInfo> ReadTopLevelTypes(
        string assemblyPath,
        CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata) return [];
        var reader = peReader.GetMetadataReader();
        var result = new List<TypeDefinitionInfo>();
        foreach (var handle in reader.TypeDefinitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var definition = reader.GetTypeDefinition(handle);
            if (!definition.GetDeclaringType().IsNil) continue;
            var name = reader.GetString(definition.Name);
            if (name == "<Module>") continue;
            var namespaceName = definition.Namespace.IsNil ? string.Empty : reader.GetString(definition.Namespace);
            var metadataName = string.IsNullOrEmpty(namespaceName) ? name : namespaceName + "." + name;
            result.Add(new TypeDefinitionInfo(handle, metadataName, definition.GetMethods().Count() + definition.GetFields().Count()));
        }

        return result;
    }

    private static TypeSelection SelectTypes(
        IReadOnlyList<TypeDefinitionInfo> types,
        AssemblyDecompilationOptions options)
    {
        var diagnostics = new List<AssemblySessionDiagnostic>();
        var allowedTypes = types.Take(options.MaxTypes).ToList();
        if (allowedTypes.Count < types.Count)
        {
            diagnostics.Add(new("assembly-type-limit", $"Die Decompilation wurde auf {options.MaxTypes} Typen begrenzt."));
        }

        var complexity = types.Sum(type => type.MemberCount);
        if (complexity > options.MaxComplexity)
        {
            allowedTypes = ApplyMemberLimit(allowedTypes, options.MaxComplexity);
            diagnostics.Add(new("assembly-complexity-limit", $"Die Assembly überschreitet die Komplexitätsgrenze ({complexity} von {options.MaxComplexity} Membern)."));
        }

        if (allowedTypes.Sum(type => type.MemberCount) > options.MaxMembers)
        {
            allowedTypes = ApplyMemberLimit(allowedTypes, options.MaxMembers);
            diagnostics.Add(new("assembly-member-limit", $"Die Decompilation wurde auf {options.MaxMembers} Member begrenzt."));
        }

        return new TypeSelection(allowedTypes, diagnostics);
    }

    private static List<TypeDefinitionInfo> ApplyMemberLimit(
        IReadOnlyList<TypeDefinitionInfo> types,
        int limit)
    {
        var result = new List<TypeDefinitionInfo>();
        var memberCount = 0;
        foreach (var type in types)
        {
            if (memberCount + type.MemberCount > limit) break;
            result.Add(type);
            memberCount += type.MemberCount;
        }

        return result;
    }

    private static void AddModuleDocumentIfRequired(
        ICSharpCode.Decompiler.CSharp.CSharpDecompiler decompiler,
        AssemblyDecompilationOptions options,
        TypeSelection selection,
        ICollection<DecompiledDocument> documents,
        CancellationToken cancellationToken)
    {
        if (documents.Count != 0 || selection.Types.Count != 0) return;
        var source = DecompileModule(decompiler, options, cancellationToken);
        if (!string.IsNullOrWhiteSpace(source)) documents.Add(new DecompiledDocument("source/00000-assembly.cs", "assembly", source));
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) || character is '.' or '+' or '`' ? '_' : character);
        }

        return builder.Length == 0 ? "assembly" : builder.ToString();
    }

    private sealed record TypeSelection(
        IReadOnlyList<TypeDefinitionInfo> Types,
        List<AssemblySessionDiagnostic> Diagnostics);

    private sealed record TypeDefinitionInfo(TypeDefinitionHandle Handle, string MetadataName, int MemberCount);
}
