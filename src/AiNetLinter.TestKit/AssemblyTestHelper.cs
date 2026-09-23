#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.TestKit;

public static class AssemblyTestHelper
{
    private static readonly TimeSpan[] WriteRetryDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromMilliseconds(25),
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500),
    ];

    public static string EmitAssembly(
        TestTempDirectory temp,
        string name,
        string source,
        params string[] additionalReferences)
        => Emit(temp, name, source, OutputKind.DynamicallyLinkedLibrary, "dll", additionalReferences);

    public static string EmitExecutable(
        TestTempDirectory temp,
        string name,
        string source,
        params string[] additionalReferences)
        => Emit(temp, name, source, OutputKind.ConsoleApplication, "exe", additionalReferences);

    private static string Emit(
        TestTempDirectory temp,
        string name,
        string source,
        OutputKind outputKind,
        string extension,
        string[] additionalReferences)
    {
        ArgumentNullException.ThrowIfNull(temp);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(additionalReferences);

        var outputPath = temp.GetPath($"{name}.{extension}");
        var references = RoslynTestSolutionFactory.CoreReferences
            .Concat(additionalReferences.Select(path => MetadataReference.CreateFromFile(path)))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(outputKind));

        using var peStream = new MemoryStream();
        var emit = compilation.Emit(peStream);
        if (!emit.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, emit.Diagnostics));
        }

        WriteFileWithRetry(outputPath, peStream);
        return outputPath;
    }

    private static void WriteFileWithRetry(string outputPath, MemoryStream stream)
    {
        for (var attempt = 0; attempt < WriteRetryDelays.Length; attempt++)
        {
            try
            {
                using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
                stream.Position = 0;
                stream.CopyTo(fileStream);
                return;
            }
            catch (IOException) when (attempt < WriteRetryDelays.Length - 1)
            {
                Thread.Sleep(WriteRetryDelays[attempt + 1]);
            }
            catch (UnauthorizedAccessException) when (attempt < WriteRetryDelays.Length - 1)
            {
                Thread.Sleep(WriteRetryDelays[attempt + 1]);
            }
        }
    }
}
