#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;
using AiNetLinter.Maps.Skeleton;

namespace AiNetLinter.Tests.Maps.Skeleton;

public sealed class SkeletonSyntaxWalkerTests
{
    private static (SkeletonSyntaxWalker Walker, SemanticModel Model) CreateWalker(string code)
    {
        var (tree, model) = TestHelper.ParseCode(code);
        var walker = new SkeletonSyntaxWalker(model, "Test.cs");
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
        Assert.Single(walker.Types);
        Assert.Equal("record", walker.Types[0].TypeKind);
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
}
