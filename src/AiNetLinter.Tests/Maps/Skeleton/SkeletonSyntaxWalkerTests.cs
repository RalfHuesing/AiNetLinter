#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;
using AiNetLinter.Maps.Skeleton;

namespace AiNetLinter.Tests.Maps.Skeleton;

public sealed class SkeletonSyntaxWalkerTests
{
    private static (SkeletonSyntaxWalker Walker, SemanticModel Model) CreateWalker(
        string code,
        System.Collections.Generic.IReadOnlyList<string>? includeNamespaces = null,
        System.Collections.Generic.IReadOnlyList<string>? excludeNamespaces = null,
        bool publicOnly = false)
    {
        var (tree, model) = TestHelper.ParseCode(code);
        var walker = new SkeletonSyntaxWalker(
            model,
            "Test.cs",
            includeNamespaces ?? System.Array.Empty<string>(),
            excludeNamespaces ?? System.Array.Empty<string>(),
            publicOnly);
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
            using System;
            public class Svc
            {
                public void Run(string s)
                {
                    if (s == null) throw new ArgumentNullException(nameof(s));
                }
            }
            """;
        var (walker, _) = CreateWalker(code);
        var method = walker.Types[0].Members
            .First(m => m.Kind == MemberKind.PublicMethod);
        Assert.NotNull(method.MetaComment);
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
        var type = Assert.Single(walker.Types);
        Assert.Equal("record", type.TypeKind);
        Assert.Equal(2, type.Members.Count);
        
        var nameProp = Assert.Single(type.Members, m => m.Signature.Contains("Name"));
        Assert.Equal(MemberKind.Property, nameProp.Kind);
        Assert.Equal("public string Name { get; init; }", nameProp.Signature);

        var ageProp = Assert.Single(type.Members, m => m.Signature.Contains("Age"));
        Assert.Equal(MemberKind.Property, ageProp.Kind);
        Assert.Equal("public int Age { get; init; }", ageProp.Signature);
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

    [Fact]
    public void ExtractsUsedDependenciesInMethod()
    {
        var code = """
            namespace Foo;
            public class Svc
            {
                private readonly object _workspace;
                private readonly object _other;

                public void Run()
                {
                    var x = _workspace;
                }
            }
            """;
        var (walker, _) = CreateWalker(code);
        var method = walker.Types[0].Members
            .First(m => m.Kind == MemberKind.PublicMethod);
        Assert.NotNull(method.MetaComment);
        Assert.Contains("Uses: _workspace", method.MetaComment);
        Assert.DoesNotContain("_other", method.MetaComment);
    }

    [Fact]
    public void Walk_WithNamespaceFilter_IgnoresExcludedNamespaces()
    {
        var code = """
            namespace Foo;
            public class MyService1 { }
            """;
        var (walker1, _) = CreateWalker(code, includeNamespaces: new[] { "Bar" });
        Assert.Empty(walker1.Types);

        var (walker2, _) = CreateWalker(code, excludeNamespaces: new[] { "Foo" });
        Assert.Empty(walker2.Types);

        var (walker3, _) = CreateWalker(code, includeNamespaces: new[] { "Foo" });
        Assert.Single(walker3.Types);
    }

    [Fact]
    public void Walk_WithPublicOnly_ExcludesPrivateMembers()
    {
        var code = """
            namespace Foo;
            public class MyService
            {
                public string PubProp { get; set; }
                private string PrivField;
                protected void ProtMethod() { }
            }
            """;
        var (walker, _) = CreateWalker(code, publicOnly: true);
        var type = Assert.Single(walker.Types);
        var memberSignatures = type.Members.Select(m => m.Signature).ToList();
        
        Assert.Contains(memberSignatures, s => s.Contains("PubProp"));
        Assert.DoesNotContain(memberSignatures, s => s.Contains("PrivField"));
        Assert.DoesNotContain(memberSignatures, s => s.Contains("ProtMethod"));
    }
}
