#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;
using Xunit;

namespace AiNetLinter.Tests;

// @covers LinterAutoFixer

/// <summary>
/// Unit-Tests für den LinterAutoFixer zur Verifizierung der Korrektur-Operationen.
/// </summary>
public sealed class AutoFixerTests
{
    private static Solution CreateTestSolution(Dictionary<string, string> files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Create(), "TestProj", "TestProj", LanguageNames.CSharp)
            .WithMetadataReferences(new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var solution = workspace.CurrentSolution.AddProject(projectInfo);

        foreach (var file in files)
        {
            var docId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(docId, file.Key, file.Value);
        }

        return solution;
    }

    [Fact]
    public async Task FixSealedClasses_SealsConcreteClassesWithoutDescendants()
    {
        var source = """
            namespace TestNamespace;
            public class SafeToSeal {}
            public class BaseClass {}
            public class DerivedClass : BaseClass {}
            """;

        var solution = CreateTestSolution(new() { ["File.cs"] = source });
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".cs");
        var document = solution.Projects.First().Documents.First();
        var solutionWithPaths = solution.WithDocumentFilePath(document.Id, tempPath);

        var violations = new List<RuleViolation>
        {
            new() { FilePath = tempPath, LineNumber = 2, RuleName = "EnforceSealedClasses", Details = "SafeToSeal", Guidance = "" },
            new() { FilePath = tempPath, LineNumber = 3, RuleName = "EnforceSealedClasses", Details = "BaseClass", Guidance = "" },
            new() { FilePath = tempPath, LineNumber = 4, RuleName = "EnforceSealedClasses", Details = "DerivedClass", Guidance = "" }
        };
        
        try
        {
            var fixedCount = await LinterAutoFixer.FixAsync(solutionWithPaths, violations, verbose: false);

            Assert.True(File.Exists(tempPath));
            var newContent = File.ReadAllText(tempPath);

            Assert.Contains("public sealed class SafeToSeal", newContent);
            Assert.Contains("public sealed class DerivedClass", newContent);
            
            Assert.DoesNotContain("public sealed class BaseClass", newContent);
            Assert.Contains("public class BaseClass", newContent);
            
            Assert.Equal(2, fixedCount);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FixSealedClasses_SealsPrivateNestedClasses()
    {
        var source = """
            namespace TestNamespace;
            public sealed class OuterClass
            {
                private class InnerClass {}
            }
            """;

        var solution = CreateTestSolution(new() { ["File.cs"] = source });
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".cs");
        var document = solution.Projects.First().Documents.First();
        var solutionWithPaths = solution.WithDocumentFilePath(document.Id, tempPath);

        var violations = new List<RuleViolation>
        {
            new() { FilePath = tempPath, LineNumber = 4, RuleName = "EnforceSealedClasses", Details = "InnerClass", Guidance = "" }
        };

        try
        {
            var fixedCount = await LinterAutoFixer.FixAsync(solutionWithPaths, violations, verbose: false);

            Assert.True(File.Exists(tempPath));
            var newContent = File.ReadAllText(tempPath);

            Assert.Contains("private sealed class InnerClass", newContent);
            Assert.Equal(1, fixedCount);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FixNullableEnable_PrependsNullableDirective()
    {
        var source = """
            namespace TestNamespace;
            public class TestClass {}
            """;

        var solution = CreateTestSolution(new() { ["File.cs"] = source });
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".cs");
        var document = solution.Projects.First().Documents.First();
        var solutionWithPath = solution.WithDocumentFilePath(document.Id, tempPath);

        var violations = new List<RuleViolation>
        {
            new() { FilePath = tempPath, LineNumber = 1, RuleName = "EnforceNullableEnable", Details = "", Guidance = "" }
        };

        try
        {
            var fixedCount = await LinterAutoFixer.FixAsync(solutionWithPath, violations, verbose: false);

            Assert.True(File.Exists(tempPath));
            var newContent = File.ReadAllText(tempPath);

            Assert.StartsWith("#nullable enable", newContent.TrimStart());
            Assert.Equal(1, fixedCount);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FixReadonlyFields_AddsReadonlyKeyword()
    {
        var source = """
            namespace TestNamespace;
            public class TestClass
            {
                private int _value;
                public TestClass() { _value = 5; }
            }
            """;

        var solution = CreateTestSolution(new() { ["File.cs"] = source });
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".cs");
        var document = solution.Projects.First().Documents.First();
        var solutionWithPath = solution.WithDocumentFilePath(document.Id, tempPath);

        var violations = new List<RuleViolation>
        {
            new() { FilePath = tempPath, LineNumber = 4, RuleName = "EnforceReadonlyFields", Details = "private int '_value'", Guidance = "" }
        };

        try
        {
            var fixedCount = await LinterAutoFixer.FixAsync(solutionWithPath, violations, verbose: false);

            Assert.True(File.Exists(tempPath));
            var newContent = File.ReadAllText(tempPath);

            Assert.Contains("private readonly int _value;", newContent);
            Assert.Equal(1, fixedCount);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
