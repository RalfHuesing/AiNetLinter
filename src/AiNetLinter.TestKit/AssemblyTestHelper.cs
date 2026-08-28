#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.TestKit;

public static class AssemblyTestHelper
{
    public static string EmitAssembly(
        TestTempDirectory temp,
        string name,
        string source,
        params string[] additionalReferences)
    {
        ArgumentNullException.ThrowIfNull(temp);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(additionalReferences);

        var outputPath = temp.GetPath(name + ".dll");
        var references = RoslynTestSolutionFactory.CoreReferences
            .Concat(additionalReferences.Select(path => MetadataReference.CreateFromFile(path)))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = compilation.Emit(outputPath);
        if (!emit.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, emit.Diagnostics));
        }

        return outputPath;
    }
}
