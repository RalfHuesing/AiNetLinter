#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core;

public sealed class MiddleManCheckerTests
{
    private static readonly string TestHelperTypes = @"
        public class Collaborator
        {
            public void DoStuff() {}
            public int Value => 42;
        }
    ";

    [Fact]
    public void MiddleManChecker_Reports_ExcessiveMiddleMan()
    {
        var code = TestHelperTypes + @"
            public class MiddleManClass
            {
                private readonly Collaborator _c = new();
                public void M1() => _c.DoStuff();
                public void M2() { _c.DoStuff(); }
                public int P1 => _c.Value;
                public int P2 { get { return _c.Value; } }
                public void M3() => _c.DoStuff();
            }
        ";

        var (tree, model) = TestHelper.ParseCode(code);
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with
            {
                Global = new GlobalConfig
                {
                    AvoidExcessiveMiddleMen = true,
                    MaxMiddleManForwardingRatio = 0.60,
                    MiddleManMinMemberCount = 5
                }
            },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MiddleManClass");

        MiddleManChecker.Check(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("AvoidExcessiveMiddleMen", ctx.Violations.First().RuleName);
    }

    [Fact]
    public void MiddleManChecker_NoViolation_UnderMinMemberCount()
    {
        var code = TestHelperTypes + @"
            public class SmallClass
            {
                private readonly Collaborator _c = new();
                public void M1() => _c.DoStuff();
                public void M2() => _c.DoStuff();
                public void M3() => _c.DoStuff();
                public void M4() => _c.DoStuff();
            }
        ";

        var (tree, model) = TestHelper.ParseCode(code);
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with
            {
                Global = new GlobalConfig
                {
                    AvoidExcessiveMiddleMen = true,
                    MaxMiddleManForwardingRatio = 0.60,
                    MiddleManMinMemberCount = 5
                }
            },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "SmallClass");

        MiddleManChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void MiddleManChecker_NoViolation_WithActualBusinessLogic()
    {
        var code = TestHelperTypes + @"
            public class BusinessClass
            {
                private readonly Collaborator _c = new();
                public void M1() => _c.DoStuff();
                public void M2() => _c.DoStuff();
                
                // Hat Logik (if-Statement) -> Keine reine Weiterleitung
                public void M3() 
                { 
                    if (P1 > 0)
                        _c.DoStuff(); 
                }
                
                // Mehrere Statements -> Keine reine Weiterleitung
                public void M4()
                {
                    var x = 10;
                    _c.DoStuff();
                }

                public int P1 => _c.Value;
            }
        ";

        var (tree, model) = TestHelper.ParseCode(code);
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with
            {
                Global = new GlobalConfig
                {
                    AvoidExcessiveMiddleMen = true,
                    MaxMiddleManForwardingRatio = 0.60,
                    MiddleManMinMemberCount = 5
                }
            },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "BusinessClass");

        MiddleManChecker.Check(node, ctx);

        // 3 von 5 Membern sind Weiterleitungen = 60%, Grenzwert ist > 60%. Sollte also leer sein.
        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void MiddleManChecker_NoViolation_ExemptSuffix()
    {
        var code = TestHelperTypes + @"
            public class TestFacade
            {
                private readonly Collaborator _c = new();
                public void M1() => _c.DoStuff();
                public void M2() => _c.DoStuff();
                public int P1 => _c.Value;
                public int P2 => _c.Value;
                public void M3() => _c.DoStuff();
            }
        ";

        var (tree, model) = TestHelper.ParseCode(code);
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with
            {
                Global = new GlobalConfig
                {
                    AvoidExcessiveMiddleMen = true,
                    MaxMiddleManForwardingRatio = 0.60,
                    MiddleManMinMemberCount = 5,
                    MiddleManExemptSuffixes = new[] { "Facade" }
                }
            },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "TestFacade");

        MiddleManChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void MiddleManChecker_NoViolation_LocalCalls()
    {
        var code = @"
            public class LocalCallClass
            {
                public void M1() => M2();
                public void M2() => M3();
                public void M3() => M4();
                public void M4() => M5();
                public void M5() {}
            }
        ";

        var (tree, model) = TestHelper.ParseCode(code);
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with
            {
                Global = new GlobalConfig
                {
                    AvoidExcessiveMiddleMen = true,
                    MaxMiddleManForwardingRatio = 0.60,
                    MiddleManMinMemberCount = 5
                }
            },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "LocalCallClass");

        MiddleManChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void MiddleManChecker_NoViolation_ExemptBaseType()
    {
        var code = TestHelperTypes + @"
            public class BaseComponent {}
            public class ChildComponent : BaseComponent
            {
                private readonly Collaborator _c = new();
                public void M1() => _c.DoStuff();
                public void M2() => _c.DoStuff();
                public int P1 => _c.Value;
                public int P2 => _c.Value;
                public void M3() => _c.DoStuff();
            }
        ";

        var (tree, model) = TestHelper.ParseCode(code);
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with
            {
                Global = new GlobalConfig
                {
                    AvoidExcessiveMiddleMen = true,
                    MaxMiddleManForwardingRatio = 0.60,
                    MiddleManMinMemberCount = 5,
                    MiddleManExemptBaseTypes = new[] { "BaseComponent" }
                }
            },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "ChildComponent");

        MiddleManChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void MiddleManChecker_PrivateIgnored_ByDefault()
    {
        var code = TestHelperTypes + @"
            public class PrivateMiddleManClass
            {
                private readonly Collaborator _c = new();
                private void M1() => _c.DoStuff();
                private void M2() => _c.DoStuff();
                private int P1 => _c.Value;
                private int P2 => _c.Value;
                private void M3() => _c.DoStuff();
            }
        ";

        var (tree, model) = TestHelper.ParseCode(code);
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with
            {
                Global = new GlobalConfig
                {
                    AvoidExcessiveMiddleMen = true,
                    MaxMiddleManForwardingRatio = 0.60,
                    MiddleManMinMemberCount = 5,
                    MiddleManIncludePrivateMembers = false // Standard
                }
            },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "PrivateMiddleManClass");

        MiddleManChecker.Check(node, ctx);

        // Weil private standardmäßig ignoriert wird, hat die Klasse 0 berücksichtigte Member.
        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void MiddleManChecker_PrivateIncluded_WhenOptionTrue()
    {
        var code = TestHelperTypes + @"
            public class PrivateMiddleManClass
            {
                private readonly Collaborator _c = new();
                private void M1() => _c.DoStuff();
                private void M2() => _c.DoStuff();
                private int P1 => _c.Value;
                private int P2 => _c.Value;
                private void M3() => _c.DoStuff();
            }
        ";

        var (tree, model) = TestHelper.ParseCode(code);
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with
            {
                Global = new GlobalConfig
                {
                    AvoidExcessiveMiddleMen = true,
                    MaxMiddleManForwardingRatio = 0.60,
                    MiddleManMinMemberCount = 5,
                    MiddleManIncludePrivateMembers = true // Aktiviert
                }
            },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "PrivateMiddleManClass");

        MiddleManChecker.Check(node, ctx);

        // Da private mitgezählt wird, hat die Klasse 5 Member, die alle Forwarder sind (100% > 60%).
        Assert.Single(ctx.Violations);
        Assert.Equal("AvoidExcessiveMiddleMen", ctx.Violations.First().RuleName);
    }

    [Fact]
    public void MiddleManChecker_ExplicitInterface_Ignored_EvenWhenOptionTrue()
    {
        var code = @"
            using System;

            public class Collaborator
            {
                public void DoStuff() {}
                public int Value => 42;
            }

            public class ExplicitInterfaceClass : IDisposable
            {
                private readonly Collaborator _c = new();
                
                // Explizite Implementierung (sollte ignoriert werden)
                void IDisposable.Dispose() => _c.DoStuff();
                
                // 4 private Forwarder
                private void M1() => _c.DoStuff();
                private void M2() => _c.DoStuff();
                private int P1 => _c.Value;
                private int P2 => _c.Value;
            }
        ";

        var (tree, model) = TestHelper.ParseCode(code);
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with
            {
                Global = new GlobalConfig
                {
                    AvoidExcessiveMiddleMen = true,
                    MaxMiddleManForwardingRatio = 0.60,
                    MiddleManMinMemberCount = 5,
                    MiddleManIncludePrivateMembers = true // Aktiviert
                }
            },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "ExplicitInterfaceClass");

        MiddleManChecker.Check(node, ctx);

        // Die explizite Interface-Implementierung wird ignoriert.
        // Es bleiben nur 4 private Member übrig. Das ist kleiner als MiddleManMinMemberCount (5).
        // Daher darf kein Verstoß gemeldet werden.
        Assert.Empty(ctx.Violations);
    }
}

