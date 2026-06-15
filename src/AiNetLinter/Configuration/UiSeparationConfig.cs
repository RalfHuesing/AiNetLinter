#nullable enable

using System;

namespace AiNetLinter.Configuration;

/// <summary>
/// Konfiguration für die UI-Datei-Trennungsregeln (Blazor CSS-Isolation, Code-Behind, WPF MVVM).
/// </summary>
public sealed record UiSeparationConfig
{
    /// <summary>
    /// Wenn true, muss jede .razor-Datei eine .razor.cs-Begleitdatei haben.
    /// Erzwingt das "Code-Behind"-Muster für Blazor (kein @code{} inline).
    /// </summary>
    public bool BlazorRequireCodeBehind { get; init; } = true;

    /// <summary>
    /// Wenn true, muss jede .razor-Datei eine .razor.css-Begleitdatei haben (CSS-Isolation).
    /// Erzwingt das Auslagern aller Styles aus der .razor-Datei.
    /// </summary>
    public bool BlazorRequireCssIsolation { get; init; } = true;

    /// <summary>
    /// Wenn true, dürfen WPF Code-Behind-Klassen (partial classes die von konfigurierten Basistypen erben)
    /// nur den Konstruktor mit InitializeComponent() enthalten.
    /// </summary>
    public bool WpfRequireMinimalCodeBehind { get; init; } = true;

    /// <summary>
    /// Basis-Typnamen die WPF Code-Behind-Klassen identifizieren.
    /// Eine partial class mit einem dieser Basistypen wird als WPF Code-Behind gewertet.
    /// </summary>
    public IReadOnlyCollection<string> WpfCodeBehindBaseTypes { get; init; } = new[]
    {
        "Window", "UserControl", "Page", "NavigationWindow"
    };

    /// <summary>
    /// Dateinamen-Muster für .razor-Dateien, die von den Blazor-Checks ausgeschlossen werden.
    /// Unterstützt exakte Namen. Beispiel: ["_Imports.razor", "App.razor"]
    /// </summary>
    public IReadOnlyCollection<string> BlazorExcludeFileNames { get; init; } = new[]
    {
        "_Imports.razor"
    };

    /// <summary>
    /// Klassen-Namens-Muster, die vom WPF-Check ausgeschlossen werden.
    /// Unterstützt exakte Klassennamen.
    /// </summary>
    public IReadOnlyCollection<string> WpfExcludeClassNames { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Wenn true (Default), wird BlazorRequireCssIsolation nur ausgelöst wenn die .razor-Datei
    /// tatsächlich native HTML-Elemente (&lt;div&gt;, &lt;span&gt; etc.) oder class=/style=-Attribute enthält.
    /// Reine Komponenten-Komposition (nur PascalCase-Tags wie &lt;MudButton&gt;) wird nicht beanstandet.
    /// </summary>
    public bool BlazorCssIsolationOnlyWhenStylesNeeded { get; init; } = true;

    /// <summary>
    /// Wendet Projekt-Overrides an.
    /// </summary>
    public UiSeparationConfig Apply(UiSeparationConfigOverride? @override)
    {
        if (@override == null) return this;
        return this with
        {
            BlazorRequireCodeBehind = @override.BlazorRequireCodeBehind ?? BlazorRequireCodeBehind,
            BlazorRequireCssIsolation = @override.BlazorRequireCssIsolation ?? BlazorRequireCssIsolation,
            WpfRequireMinimalCodeBehind = @override.WpfRequireMinimalCodeBehind ?? WpfRequireMinimalCodeBehind,
            WpfCodeBehindBaseTypes = @override.WpfCodeBehindBaseTypes ?? WpfCodeBehindBaseTypes,
            BlazorExcludeFileNames = @override.BlazorExcludeFileNames ?? BlazorExcludeFileNames,
            WpfExcludeClassNames = @override.WpfExcludeClassNames ?? WpfExcludeClassNames,
            BlazorCssIsolationOnlyWhenStylesNeeded = @override.BlazorCssIsolationOnlyWhenStylesNeeded ?? BlazorCssIsolationOnlyWhenStylesNeeded,
        };
    }
}

/// <summary>
/// Optionale Überschreibungen für UiSeparationConfig.
/// </summary>
public sealed record UiSeparationConfigOverride
{
    public bool? BlazorRequireCodeBehind { get; init; }
    public bool? BlazorRequireCssIsolation { get; init; }
    public bool? WpfRequireMinimalCodeBehind { get; init; }
    public IReadOnlyCollection<string>? WpfCodeBehindBaseTypes { get; init; }
    public IReadOnlyCollection<string>? BlazorExcludeFileNames { get; init; }
    public IReadOnlyCollection<string>? WpfExcludeClassNames { get; init; }
    public bool? BlazorCssIsolationOnlyWhenStylesNeeded { get; init; }
}
