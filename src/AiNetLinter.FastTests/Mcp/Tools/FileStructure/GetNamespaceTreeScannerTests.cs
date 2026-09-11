#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

[Trait("Category", "Unit")]
public sealed class GetNamespaceTreeScannerTests
{
    [Fact]
    public async Task ScanSolutionProjectsAsync_ReturnsAllProjectsWithClassificationAndCounts()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec(
                "App.Core",
                [("Model.cs", "namespace App.Core; public class MyModel {}")],
                OutputKind: OutputKind.DynamicallyLinkedLibrary),
            new ProjectSpec(
                "App.Cli",
                [("Program.cs", "namespace App.Cli; public class Program {}")],
                OutputKind: OutputKind.ConsoleApplication),
            new ProjectSpec(
                "App.Tests",
                [("MyTest.cs", "namespace App.Tests; public class MyTest {}")],
                OutputKind: OutputKind.DynamicallyLinkedLibrary));

        var (text, payload) = await GetNamespaceTreeScanner.ScanSolutionProjectsAsync(
            testSolution.Solution, CancellationToken.None);

        Assert.Contains("# Solution Overview: MySolution.slnx (3 Projekte)", text);
        Assert.Contains("App.Core (Typ: Lib, 1 Namespaces, 1 Typen)", text);
        Assert.Contains("App.Cli (Typ: Exe, 1 Namespaces, 1 Typen)", text);
        Assert.Contains("App.Tests (Typ: Test, 1 Namespaces, 1 Typen)", text);

        Assert.NotNull(payload.Projects);
        Assert.Equal(3, payload.Projects!.Count);
        Assert.Equal("Lib", payload.Projects[0].ProjectType);
        Assert.Equal("Exe", payload.Projects[1].ProjectType);
        Assert.Equal("Test", payload.Projects[2].ProjectType);
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_Level2_ReturnsNamespaceTree()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec(
                "App.Core",
                [
                    ("Model.cs", "namespace App.Core.Models; public class MyModel {}"),
                    ("Service.cs", "namespace App.Core.Services; public class MyService {}"),
                    ("SubService.cs", "namespace App.Core.Services.Sub; public class MySubService {}"),
                ]));

        var project = testSolution.Solution.Projects.Single();
        var parameters = new NamespaceTreeScanParameters(
            Project: project,
            NamespacePrefix: null,
            Depth: 3,
            IncludeTypes: false,
            KindFilter: "all",
            MaxResults: 50,
            SolutionDir: @"C:\virtual");

        var (text, payload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            parameters,
            ct: CancellationToken.None);

        Assert.Contains("# Namespaces in Projekt 'App.Core':", text);
        Assert.Contains("App.Core.Models (1 Typen)", text);
        Assert.Contains("App.Core.Services (1 Typen)", text);
        Assert.Contains("App.Core.Services.Sub (1 Typen)", text);

        Assert.NotNull(payload.Namespaces);
        Assert.NotEmpty(payload.Namespaces!);
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_Level3_ReturnsTypesWithKindFilter()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec(
                "App.Core",
                [
                    ("Entities.cs", """
                        namespace App.Core.Domain;
                        public class UserClass {}
                        public interface IUserInterface {}
                        public record UserRecord(string Name);
                        public struct UserStruct {}
                        public enum UserEnum { A, B }
                        """),
                ]));

        var project = testSolution.Solution.Projects.Single();
        var parameters = new NamespaceTreeScanParameters(
            Project: project,
            NamespacePrefix: "App.Core.Domain",
            Depth: 1,
            IncludeTypes: true,
            KindFilter: "interface",
            MaxResults: 50,
            SolutionDir: @"C:\virtual");

        var (classText, classPayload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            parameters,
            ct: CancellationToken.None);

        Assert.Contains("# Typen in Namespace 'App.Core.Domain' (Projekt: App.Core):", classText);
        Assert.Contains("IUserInterface (interface)", classText);
        Assert.DoesNotContain("UserClass", classText);

        Assert.NotNull(classPayload.Types);
        Assert.Single(classPayload.Types!);
        Assert.Equal("IUserInterface", classPayload.Types![0].Name);
        Assert.Equal("interface", classPayload.Types[0].Kind);
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_ExactLeafNamespaceRemainsStructuredRoot()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec("App.Core", [("Leaf.cs", "namespace App.Leaf; public class LeafType {}")]));
        var project = testSolution.Solution.Projects.Single();

        var (_, payload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            new NamespaceTreeScanParameters(project, "App.Leaf", 1, true, "all", 50, @"C:\virtual"),
            CancellationToken.None);

        var root = Assert.Single(payload.Namespaces!);
        Assert.Equal("App.Leaf", root.Namespace);
        Assert.Single(root.Types!);
        Assert.Null(root.SubNamespaces);
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_ExactRootKeepsDirectTypesAlongsideChildren()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec("App.Core",
            [
                ("Root.cs", "namespace App.Root; public class RootType {}"),
                ("Child.cs", "namespace App.Root.Child; public class ChildType {}"),
            ]));
        var project = testSolution.Solution.Projects.Single();

        var (_, payload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            new NamespaceTreeScanParameters(project, "App.Root", 2, true, "all", 50, @"C:\virtual"),
            CancellationToken.None);

        var root = Assert.Single(payload.Namespaces!);
        Assert.Contains(root.Types!, type => type.Name == "RootType");
        Assert.Contains(root.SubNamespaces!, child => child.Namespace == "App.Root.Child");
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_EmptyExactNamespaceRemainsRootWithChildren()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec("App.Core", [("Child.cs", "namespace App.Empty.Child; public class ChildType {}")]));
        var project = testSolution.Solution.Projects.Single();

        var (_, payload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            new NamespaceTreeScanParameters(project, "App.Empty", 1, true, "all", 50, @"C:\virtual"),
            CancellationToken.None);

        var root = Assert.Single(payload.Namespaces!);
        Assert.Equal("App.Empty", root.Namespace);
        Assert.Empty(root.Types!);
        Assert.Contains(root.SubNamespaces!, child => child.Namespace == "App.Empty.Child");
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_TrailingDotInNamespacePrefixMatchesCanonicalPrefix()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec(
                "App.Core",
                [("Entities.cs", "namespace App.Core.Domain; public class User {}")]));

        var project = testSolution.Solution.Projects.Single();
        static NamespaceTreeScanParameters Parameters(Microsoft.CodeAnalysis.Project project, string prefix) =>
            new(project, prefix, 1, true, "all", 50, @"C:\virtual");

        var (_, withoutDot) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            Parameters(project, "App.Core.Domain"), CancellationToken.None);
        var (_, withDot) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            Parameters(project, "App.Core.Domain."), CancellationToken.None);

        Assert.NotNull(withoutDot.Types);
        Assert.NotNull(withDot.Types);
        Assert.Equal(withoutDot.Types!.Select(type => type.Name), withDot.Types!.Select(type => type.Name));
        Assert.Single(withDot.Types);
        Assert.Equal("User", withDot.Types[0].Name);
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_ExcludesCompilerGeneratedAndSyntheticTypes()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec(
                "App.Core",
                [
                    ("RecordDoc.cs", """
                        namespace App.Core;
                        public record Person(string Name, int Age);
                        """),
                ]));

        var project = testSolution.Solution.Projects.Single();
        var parameters = new NamespaceTreeScanParameters(
            Project: project,
            NamespacePrefix: "App.Core",
            Depth: 1,
            IncludeTypes: true,
            KindFilter: "all",
            MaxResults: 50,
            SolutionDir: @"C:\virtual");

        var (text, payload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            parameters,
            ct: CancellationToken.None);

        Assert.Contains("Person", text);
        Assert.DoesNotContain("<Clone>$", text);
        Assert.DoesNotContain("EqualityContract", text);
        Assert.NotNull(payload.Types);
        Assert.Single(payload.Types!);
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_TruncatesWhenExceedingMaxResults()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec(
                "App.Core",
                [
                    ("Types.cs", """
                        namespace App.Core;
                        public class Type1 {}
                        public class Type2 {}
                        public class Type3 {}
                        """),
                ]));

        var project = testSolution.Solution.Projects.Single();
        var parameters = new NamespaceTreeScanParameters(
            Project: project,
            NamespacePrefix: "App.Core",
            Depth: 1,
            IncludeTypes: true,
            KindFilter: "all",
            MaxResults: 2,
            SolutionDir: @"C:\virtual");

        var (text, payload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            parameters,
            ct: CancellationToken.None);

        Assert.True(payload.Truncated);
        Assert.Equal(3, payload.TotalCount);
        Assert.Equal(2, payload.ShownCount);
        Assert.Contains("[3 Typen gesamt, 2 gezeigt — maxResults erhoehen]", text);
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_TruncatesStructuredNamespacesToMaxResults()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec(
                "App.Core",
                [
                    ("TypeA.cs", "namespace App.Core.A; public class TypeA {}"),
                    ("TypeB.cs", "namespace App.Core.B; public class TypeB {}"),
                    ("TypeC.cs", "namespace App.Core.C; public class TypeC {}"),
                ]));

        var project = testSolution.Solution.Projects.Single();
        var parameters = new NamespaceTreeScanParameters(
            Project: project,
            NamespacePrefix: "App.Core",
            Depth: 1,
            IncludeTypes: false,
            KindFilter: "all",
            MaxResults: 2,
            SolutionDir: @"C:\virtual");

        var (_, payload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            parameters,
            ct: CancellationToken.None);

        Assert.True(payload.Truncated);
        Assert.Equal(3, payload.TotalCount);
        Assert.Equal(2, payload.ShownCount);
        Assert.NotNull(payload.Namespaces);
        var root = Assert.Single(payload.Namespaces!);
        Assert.Equal("App.Core", root.Namespace);
        Assert.NotNull(root.SubNamespaces);
        Assert.Equal(2, root.SubNamespaces!.Count);
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_GlobalNamespace_ReturnsTypesInGlobalNamespace()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec(
                "App.Core",
                [
                    ("TopLevel.cs", """
                        public class GlobalRootClass {}
                        """),
                ]));

        var project = testSolution.Solution.Projects.Single();
        var parameters = new NamespaceTreeScanParameters(
            Project: project,
            NamespacePrefix: null,
            Depth: 1,
            IncludeTypes: true,
            KindFilter: "all",
            MaxResults: 50,
            SolutionDir: @"C:\virtual");

        var (text, payload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            parameters,
            ct: CancellationToken.None);

        Assert.Contains("App.Core", text);
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_KindFilterCaseInsensitive_MatchesCorrectTypes()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec(
                "App.Core",
                [
                    ("Entities.cs", """
                        namespace App.Core.Domain;
                        public struct MyStruct {}
                        public enum MyEnum { X }
                        """),
                ]));

        var project = testSolution.Solution.Projects.Single();
        var parameters = new NamespaceTreeScanParameters(
            Project: project,
            NamespacePrefix: "App.Core.Domain",
            Depth: 1,
            IncludeTypes: true,
            KindFilter: "STRUCT",
            MaxResults: 50,
            SolutionDir: @"C:\virtual");

        var (text, payload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            parameters,
            ct: CancellationToken.None);

        Assert.Contains("MyStruct (struct)", text);
        Assert.DoesNotContain("MyEnum", text);
        Assert.NotNull(payload.Types);
        Assert.Single(payload.Types!);
        Assert.Equal("MyStruct", payload.Types![0].Name);
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_EmptyParentWithSubNamespaceTypes_Navigable()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec(
                "App.Core",
                [
                    ("Deep.cs", """
                        namespace App.Core.Services.Sub;
                        public class DeepService {}
                        """),
                ]));

        var project = testSolution.Solution.Projects.Single();
        var parameters = new NamespaceTreeScanParameters(
            Project: project,
            NamespacePrefix: "App.Core",
            Depth: 2,
            IncludeTypes: false,
            KindFilter: "all",
            MaxResults: 50,
            SolutionDir: @"C:\virtual");

        var (text, payload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            parameters,
            ct: CancellationToken.None);

        Assert.Contains("App.Core.Services", text);
        Assert.NotNull(payload.Namespaces);
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_IsolatesTypesToRequestedProject_ExcludesReferencedProjectTypes()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec(
                "App.Core",
                [("CoreModel.cs", "namespace App.Core; public class CoreModel {}")]),
            new ProjectSpec(
                "App.Tests",
                [("TestClass.cs", "namespace App.Tests; public class TestClass {}")],
                ProjectReferences: ["App.Core"]));

        var testsProject = testSolution.Solution.Projects.First(p => p.Name == "App.Tests");
        var parameters = new NamespaceTreeScanParameters(
            Project: testsProject,
            NamespacePrefix: null,
            Depth: 2,
            IncludeTypes: false,
            KindFilter: "all",
            MaxResults: 50,
            SolutionDir: @"C:\virtual");

        var (text, payload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            parameters,
            ct: CancellationToken.None);

        Assert.Contains("App.Tests", text);
        Assert.DoesNotContain("App.Core", text);

        var (solutionText, solutionPayload) = await GetNamespaceTreeScanner.ScanSolutionProjectsAsync(
            testSolution.Solution, CancellationToken.None);

        Assert.Contains("App.Tests (Typ: Test, 1 Namespaces, 1 Typen)", solutionText);
        Assert.Contains("App.Core (Typ: Lib, 1 Namespaces, 1 Typen)", solutionText);
    }

    [Fact]
    public async Task ScanProjectNamespacesAsync_Level3_IncludesSubNamespaceGuidanceHintWhenSubNamespacesExist()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MySolution.slnx",
            new ProjectSpec(
                "App.Core",
                [
                    ("RootClass.cs", "namespace App.Core; public class RootClass {}"),
                    ("SubClass.cs", "namespace App.Core.Sub; public class SubClass {}"),
                ]));

        var project = testSolution.Solution.Projects.Single();
        var parameters = new NamespaceTreeScanParameters(
            Project: project,
            NamespacePrefix: "App.Core",
            Depth: 1,
            IncludeTypes: true,
            KindFilter: "all",
            MaxResults: 50,
            SolutionDir: @"C:\virtual");

        var (text, payload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(
            parameters,
            ct: CancellationToken.None);

        Assert.Contains("RootClass (class)", text);
        Assert.Contains("[Hinweis: Unter 'App.Core' existieren 1 weitere Sub-Namespaces (App.Core.Sub)", text);
    }
}
