#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace AiNetLinter.FastTests.Architecture;

/// <summary>
/// Statische Deny-Liste gegen die kompilierten Metadaten von AiNetLinter.FastTests.dll und
/// AiNetLinter.TestKit.dll. Prueft
/// AssemblyRef-, TypeRef- und MemberRef-Tabellen ueber System.Reflection.Metadata statt
/// Quelltext-Grep, damit auch indirekte Nutzung ueber eigene Helfer erkannt wird. Die
/// Produktreferenz macht MSBuild-Typen transitiv erreichbar; diese Deny-Liste ist deshalb kein
/// Kosmetikpunkt, sondern der eigentliche Schutz der Fast-Policy.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FastTestsDependencyGuardTests
{
    private static readonly string[] DeniedAssemblyNamePrefixes =
    {
        "Microsoft.Build",
        "Microsoft.CodeAnalysis.Workspaces.MSBuild",
    };

    [Fact]
    public void FastTestsAssembly_DoesNotReferenceDeniedInfrastructure()
    {
        var path = FindOutputAssembly("AiNetLinter.FastTests.dll");
        var violations = ScanForDeniedReferences(path);

        Assert.True(violations.Count == 0,
            $"AiNetLinter.FastTests.dll referenziert verbotene Infrastruktur: {string.Join(", ", violations)}");
    }

    [Fact]
    public void TestKitAssembly_DoesNotReferenceDeniedInfrastructure()
    {
        var path = FindOutputAssembly("AiNetLinter.TestKit.dll");
        var violations = ScanForDeniedReferences(path);

        Assert.True(violations.Count == 0,
            $"AiNetLinter.TestKit.dll referenziert verbotene Infrastruktur: {string.Join(", ", violations)}");
    }

    internal static List<string> ScanForDeniedReferences(string assemblyPath)
    {
        var violations = new List<string>();

        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();

        foreach (var handle in reader.AssemblyReferences)
        {
            var name = reader.GetString(reader.GetAssemblyReference(handle).Name);
            if (DeniedAssemblyNamePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            {
                violations.Add($"AssemblyRef:{name}");
            }
        }

        foreach (var handle in reader.TypeReferences)
        {
            var typeRef = reader.GetTypeReference(handle);
            var ns = reader.GetString(typeRef.Namespace);
            var name = reader.GetString(typeRef.Name);

            if (ns.StartsWith("Microsoft.Build", StringComparison.Ordinal) ||
                ns.Equals("Microsoft.CodeAnalysis.MSBuild", StringComparison.Ordinal) ||
                name.Equals("MSBuildWorkspace", StringComparison.Ordinal) ||
                (ns.Equals("System.Diagnostics", StringComparison.Ordinal) && name.Equals("Process", StringComparison.Ordinal)))
            {
                violations.Add($"TypeRef:{ns}.{name}");
            }
        }

        foreach (var handle in reader.MemberReferences)
        {
            var memberRef = reader.GetMemberReference(handle);
            var memberName = reader.GetString(memberRef.Name);
            if (memberName != "LoadAsync")
            {
                continue;
            }

            var parentTypeName = ResolveMemberParentTypeName(reader, memberRef.Parent);
            if (parentTypeName == "SourceFileCatalog")
            {
                violations.Add("MemberRef:SourceFileCatalog.LoadAsync");
            }
        }

        return violations;
    }

    private static string? ResolveMemberParentTypeName(MetadataReader reader, EntityHandle parent)
    {
        return parent.Kind switch
        {
            HandleKind.TypeReference => reader.GetString(reader.GetTypeReference((TypeReferenceHandle)parent).Name),
            HandleKind.TypeDefinition => reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)parent).Name),
            _ => null,
        };
    }

    private static string FindOutputAssembly(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(path))
        {
            return path;
        }

        throw new FileNotFoundException(
            $"Erwartete Ausgabeassembly '{fileName}' nicht im Testausgabeverzeichnis gefunden: {AppContext.BaseDirectory}");
    }
}
