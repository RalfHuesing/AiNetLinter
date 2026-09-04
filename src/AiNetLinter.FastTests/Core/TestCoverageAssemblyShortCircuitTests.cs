#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace AiNetLinter.FastTests.Core;

[Trait("Category", "Unit")]
// @covers TestDetector
// @covers TestCoverageScanner
public sealed class TestCoverageAssemblyShortCircuitTests
{
    [Fact]
    public void HasTestFrameworkReferences_IdentifiesKnownFrameworks()
    {
        var workspace = new AdhocWorkspace();
        var coreRef = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var xunitRef = MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location);

        var noTestProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("test-proj"),
            VersionStamp.Create(),
            "NoTest",
            "NoTest",
            LanguageNames.CSharp,
            metadataReferences: [coreRef]);
        var noTestProject = workspace.AddProject(noTestProjectInfo);
        Assert.False(TestDetector.HasTestFrameworkReferences(noTestProject));

        var testProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("test-proj-2"),
            VersionStamp.Create(),
            "WithTest",
            "WithTest",
            LanguageNames.CSharp,
            metadataReferences: [coreRef, xunitRef]);
        var testProject = workspace.AddProject(testProjectInfo);
        Assert.True(TestDetector.HasTestFrameworkReferences(testProject));
    }

    [Fact]
    public void IsTestProjectOrHasTestFiles_DecompiledAssemblyWithoutTestReferences_ReturnsFalseEvenWithTestNamedFile()
    {
        var workspace = new AdhocWorkspace();
        var coreRef = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        var projectId = ProjectId.CreateNewId("decompiled-assembly");
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "Vendor.Data",
            "Vendor.Data",
            LanguageNames.CSharp,
            filePath: @"C:\virtual\decompiled-assembly.csproj",
            metadataReferences: [coreRef]);
        var project = workspace.AddProject(projectInfo);

        project = project.AddDocument("HitTest.cs", "namespace Vendor.Data; public class HitTest { }", filePath: @"C:\virtual\HitTest.cs").Project;
        project = project.AddDocument("TypeTest.cs", "namespace Vendor.Data; public class TypeTest { }", filePath: @"C:\virtual\TypeTest.cs").Project;

        Assert.True(TestDetector.IsDecompiledAssemblyProject(project));
        Assert.False(TestDetector.HasTestFrameworkReferences(project));
        Assert.False(TestDetector.IsTestProject(project));
        Assert.False(TestDetector.IsTestProjectOrHasTestFiles(project));
    }

    [Fact]
    public async Task FindTestsForSymbolAsync_DecompiledAssemblyWithoutTestReferences_ShortCircuitsFast()
    {
        var workspace = new AdhocWorkspace();
        var coreRef = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        var projectId = ProjectId.CreateNewId("decompiled-assembly");
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "Vendor.Data",
            "Vendor.Data",
            LanguageNames.CSharp,
            filePath: @"C:\virtual\decompiled-assembly.csproj",
            metadataReferences: [coreRef]);

        var solution = workspace.AddProject(projectInfo).Solution;
        var docId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(docId, "Command.cs", """
            namespace Vendor.Data;
            public class Command
            {
                public void Execute() { }
            }
            """, filePath: @"C:\virtual\Command.cs");

        // Add dummy class matching test naming convention to ensure short-circuit prevents document iteration
        var testDocId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(testDocId, "CommandTest.cs", """
            namespace Vendor.Data;
            public class CommandTest
            {
                public void Execute_Runs() { }
            }
            """, filePath: @"C:\virtual\CommandTest.cs");

        var project = solution.GetProject(projectId)!;
        var compilation = await project.GetCompilationAsync(CancellationToken.None);
        var symbol = compilation!.GetTypeByMetadataName("Vendor.Data.Command")!.GetMembers("Execute").First();

        var stopwatch = Stopwatch.StartNew();
        var result = await TestCoverageScanner.FindTestsForSymbolAsync(symbol, solution, CancellationToken.None);
        stopwatch.Stop();

        Assert.Equal(0, result.TotalMatchingTests);
        Assert.Empty(result.TestFiles);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Short-circuit took too long: {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task FindTestsForSymbolAsync_DecompiledAssemblyWithTestReferences_FindsTests()
    {
        var workspace = new AdhocWorkspace();
        var coreRef = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var xunitRef = MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location);

        var projectId = ProjectId.CreateNewId("decompiled-assembly");
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "Vendor.Tests",
            "Vendor.Tests",
            LanguageNames.CSharp,
            filePath: @"C:\virtual\decompiled-assembly.csproj",
            metadataReferences: [coreRef, xunitRef]);

        var solution = workspace.AddProject(projectInfo).Solution;
        solution = solution.AddDocument(DocumentId.CreateNewId(projectId), "Target.cs", """
            namespace Vendor.Tests;
            public class Target
            {
                public void Run() { }
            }
            """, filePath: @"C:\virtual\Target.cs");

        solution = solution.AddDocument(DocumentId.CreateNewId(projectId), "TargetTests.cs", """
            using Xunit;
            namespace Vendor.Tests;
            public class TargetTests
            {
                [Fact]
                public void Run_Works()
                {
                    new Target().Run();
                }
            }
            """, filePath: @"C:\virtual\TargetTests.cs");

        var project = solution.GetProject(projectId)!;
        var compilation = await project.GetCompilationAsync(CancellationToken.None);
        var symbol = compilation!.GetTypeByMetadataName("Vendor.Tests.Target")!.GetMembers("Run").First();

        var result = await TestCoverageScanner.FindTestsForSymbolAsync(symbol, solution, CancellationToken.None);

        Assert.True(result.TotalMatchingTests >= 1);
        Assert.NotEmpty(result.TestFiles);
    }
}
