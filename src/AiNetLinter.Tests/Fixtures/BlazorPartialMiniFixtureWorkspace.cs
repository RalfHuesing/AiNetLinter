#nullable enable
using System.IO;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Isolierte Temp-Kopie des BlazorPartialMini-Fixtures — eine Sdk.Razor-Mini-Solution mit
/// einer .razor-Komponente ohne @inherits und passender .razor.cs-Codebehind-Partial-Klasse
/// mit override-Lifecycle-Methoden, aber ohne expliziten Basistyp (Blazor-Konvention).
/// </summary>
public sealed class BlazorPartialMiniFixtureWorkspace : FixtureWorkspaceBase
{
    public BlazorPartialMiniFixtureWorkspace()
        : base("BlazorPartialMini", "ainetlinter-blazorpartial-mini-")
    {
    }

    public string SiteViewCsPath => Path.Combine(RootPath, "src", "BlazorPartialMini", "SiteView.razor.cs");
}
