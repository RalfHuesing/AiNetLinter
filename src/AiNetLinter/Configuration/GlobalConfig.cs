#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Globale Verhaltensregeln und strukturelle Einschränkungen.
/// </summary>
public sealed record GlobalConfig
{
    public bool EnforceSealedClasses { get; init; } = true;
    public bool AllowUnsealedPartialClasses { get; init; } = false;
    public bool AllowDynamic { get; init; } = false;
    public bool AllowOutParameters { get; init; } = false;
    public bool EnforceValueObjectContracts { get; init; } = true;
    public bool EnableTestSentinel { get; init; } = true;
    public bool EnforcePascalCase { get; init; } = true;
    public bool EnforceAsciiIdentifiers { get; init; } = true;
    public bool EnforceXmlDocumentation { get; init; } = false;
    public bool EnforceSemanticNaming { get; init; } = true;
    public bool EnforceNullableEnable { get; init; } = true;
    public bool EnforceNoSilentCatch { get; init; } = true;
    public bool AllowTryPatternOutParameters { get; init; } = true;
    public bool AllowCancellationShutdownCatch { get; init; } = true;

    /// <summary>
    /// Wenn true: <c>out</c>-Parameter in privaten Methoden werden nicht gemeldet.
    /// Nützlich wenn private Hilfsmethoden intern das <c>out</c>-Idiom nutzen,
    /// die öffentliche API aber trotzdem sauber gehalten werden soll.
    /// Standard: true (private Methoden sind Implementierungsdetails).
    /// </summary>
    public bool AllowOutParametersInPrivateMethods { get; init; } = true;

    /// <summary>
    /// Methoden-Namen, für die <c>EnforceSemanticNaming</c> nicht geprüft wird.
    /// Nützlich für Standard-C#-Overrides wie <c>Equals(object? obj)</c> oder
    /// <c>CompareTo(object? obj)</c>, bei denen der Parametername konventionell ist.
    /// Standard: ["Equals", "CompareTo", "GetHashCode"] (BCL-Overrides).
    /// </summary>
    public IReadOnlyCollection<string> SemanticNamingExemptMethodNames { get; init; }
        = ["Equals", "CompareTo", "GetHashCode"];

    /// <summary>
    /// Wenn true: Ein Parameter-Name wird von <c>EnforceSemanticNaming</c> nicht gemeldet,
    /// wenn er als Teilstring (case-insensitiv) im Methoden-Namen vorkommt.
    /// Beispiel: Parameter "item" in Methode "AppendTimelineItemAsync" → nicht flaggen.
    /// Standard: true (semantisch korrekt wenn der Kontext im Methodennamen steckt).
    /// </summary>
    public bool SemanticNamingAllowSubstringOfMethodName { get; init; } = true;

    /// <summary>
    /// Exception-Typen, die lautlos abgefangen werden dürfen (leerer catch-Block ohne Variable).
    /// Analogon zu AllowCancellationShutdownCatch für projektspezifische Exception-Typen.
    /// Nur der einfache Typname, kein Namespace (z.B. "JSDisconnectedException").
    /// </summary>
    public IReadOnlyList<string> AllowedSilentCatchExceptionTypes { get; init; } = ["ObjectDisposedException"];
    public bool EnforceMinimalApiAsParameters { get; init; } = false;
    public bool EnforceResultPatternOverExceptions { get; init; } = false;

    /// <summary>
    /// Namespace-Suffixe für die throw erlaubt ist (z. B. "Infrastructure", "Middleware").
    /// Segment-basierter Match: "MyApp.Infrastructure" endet with ".Infrastructure".
    /// </summary>
    public IReadOnlyCollection<string> ResultPatternAllowThrowInNamespaceSuffixes { get; init; }
        = ["Infrastructure", "Endpoints", "Middleware", "Program"];

    /// <summary>
    /// Bare "throw;" (Rethrow in Catch-Block) ist immer erlaubt wenn true.
    /// </summary>
    public bool ResultPatternAllowCatchRethrow { get; init; } = true;
    public bool EnforceExplicitStateImmutability { get; init; } = false;
    public IReadOnlyCollection<string> AllowedExceptions { get; init; } = new[]
    {
        "ArgumentException",
        "ArgumentNullException",
        "ArgumentOutOfRangeException",
        "InvalidOperationException",
        "NotSupportedException",
        "KeyNotFoundException",
        "IndexOutOfRangeException",
        "TimeoutException",
        "ObjectDisposedException",
        "NotImplementedException"
    };
    public bool PreventContextDependentOverloads { get; init; } = false;
    public bool EnforceNamespaceDirectoryMapping { get; init; } = true;

    /// <summary>
    /// Der Modus fuer den Namespace-Ordner-Abgleich: "exact" | "suffix-match" | "contains-all"
    /// </summary>
    public string NamespaceDirectoryMappingMode { get; init; } = "suffix-match";

    /// <summary>
    /// Pfad-Segmente, die beim Namespace-Vergleich ignoriert werden.
    /// </summary>
    public IReadOnlyCollection<string> NamespaceDirectoryMappingIgnorePathSegments { get; init; }
        = ["src", "Source", "Domains", "Handlers"];

    /// <summary>
    /// Im Suffix-Match-Modus: Anzahl der letzten Ordner-Segmente, die im Namespace enthalten sein muessen.
    /// </summary>
    public int NamespaceDirectoryMappingRequiredTrailingSegments { get; init; } = 2;

    public bool DetectAndBanPhantomDependencies { get; init; } = false;

    /// <summary>
    /// Verbietet oeffentliche (public/internal) nested Typen innerhalb von Klassen, Records und Structs.
    /// Verbessert die Grep-/File-Listing-Navigation fuer KI-Agenten und verhindert FQN-Halluzinationen
    /// (z. B. <c>PaymentStatus</c> statt <c>PaymentProcessor.PaymentStatus</c>).
    /// Standard: <c>true</c>. Private nested Typen bleiben erlaubt (Implementierungsdetail).
    /// </summary>
    public bool BanPublicNestedTypes { get; init; } = true;

    /// <summary>
    /// Wenn <c>true</c> (Standard): <c>private</c> nested Typen bleiben erlaubt, weil sie kein
    /// externes Grep-Target fuer Agenten darstellen. Auf <c>false</c> setzen, um auch private
    /// nested Typen zu melden (strikter Greenfield-Modus).
    /// </summary>
    public bool BanPublicNestedTypesAllowPrivate { get; init; } = true;

    /// <summary>
    /// Erkennt und meldet Klassen, die primär als Weiterleitungsschicht ("Middle Man") agieren,
    /// da sie die Indirektionstiefe für Agenten unnötig erhöhen.
    /// </summary>
    public bool AvoidExcessiveMiddleMen { get; init; } = true;

    /// <summary>
    /// Grenzwert für das Verhältnis von reinen Weiterleitungsmethoden/-properties zur Gesamtanzahl.
    /// Standard: 0.60 (60%).
    /// </summary>
    public double MaxMiddleManForwardingRatio { get; init; } = 0.60;

    /// <summary>
    /// Mindestanzahl nicht-privater Mitglieder einer Klasse, ab der die Middle-Man-Regel greift.
    /// Kleine Wrapper/Adapter (z.B. mit weniger als 5 Membern) werden ignoriert.
    /// Standard: 5.
    /// </summary>
    public int MiddleManMinMemberCount { get; init; } = 5;

    /// <summary>
    /// Wenn <c>true</c>, werden auch private Methoden und Properties für die Middle-Man-Analyse
    /// berücksichtigt (Standard: <c>false</c>).
    /// </summary>
    public bool MiddleManIncludePrivateMembers { get; init; } = false;


    /// <summary>
    /// Klassenname-Suffixe, die vom Middle-Man-Check ausgenommen sind.
    /// Standard: ["Extensions", "Proxy", "Adapter", "Facade"].
    /// </summary>
    public IReadOnlyCollection<string> MiddleManExemptSuffixes { get; init; }
        = ["Extensions", "Proxy", "Adapter", "Facade"];

    /// <summary>
    /// Basisklassen oder Schnittstellen, bei deren Implementierung eine Klasse vom Middle-Man-Check ausgenommen ist.
    /// Standard: ["ComponentBase", "LayoutComponentBase"].
    /// </summary>
    public IReadOnlyCollection<string> MiddleManExemptBaseTypes { get; init; }
        = ["ComponentBase", "LayoutComponentBase"];

    public IReadOnlyCollection<string> ImmutabilityExemptSuffixes { get; init; } = new[]
    {
        "Dto", "Entity", "Model", "Request", "Response", "Command"
    };
    public IReadOnlyCollection<string> ImmutabilityExemptPatterns { get; init; } = Array.Empty<string>();
    public bool AllowedEmptyReads { get; init; } = false;
    public IReadOnlyCollection<string> SealedClassExemptSuffixes { get; init; } = ["Base", "Foundation", "Host"];
    public IReadOnlyCollection<string> ImmutabilityExemptBaseTypes { get; init; } =
    [
        "ComponentBase",
        "LayoutComponentBase",
        "ObservableObject",
        "ObservableRecipient",
        "BackgroundService",
        "AuthenticationStateProvider",
        "INotifyPropertyChanged"
    ];
    public bool ImmutabilityAllowPrivateBackingFields { get; init; } = true;
    public bool EnablePerformanceProfiling { get; init; } = true;

    /// <summary>
    /// Verbietet <c>async void</c>-Methoden und lokale Funktionen.
    /// Das einzige legitime Einsatzszenario — Event-Handler mit der Signatur
    /// <c>(object sender, EventArgs e)</c> — wird via <see cref="AsyncVoidAllowEventHandlers"/> gesteuert.
    /// Standard: <c>true</c>.
    /// </summary>
    public bool BanAsyncVoid { get; init; } = true;

    /// <summary>
    /// Wenn <c>true</c>: <c>async void</c>-Methoden, deren Parameter-Liste exakt
    /// <c>(object sender, EventArgs e)</c> entspricht (oder eine EventArgs-Unterklasse enthält),
    /// werden nicht gemeldet.
    /// Standard: <c>true</c> (idiomatische Event-Handler bleiben erlaubt).
    /// </summary>
    public bool AsyncVoidAllowEventHandlers { get; init; } = true;

    /// <summary>
    /// Verbietet blockierende Task-Zugriffe: <c>.Wait()</c>, <c>.Result</c>
    /// und <c>.GetAwaiter().GetResult()</c> auf <c>Task</c>- und <c>ValueTask</c>-Instanzen.
    /// Standard: <c>true</c>.
    /// </summary>
    public bool BanBlockingTaskAccess { get; init; } = true;

    /// <summary>
    /// Wenn <c>true</c>: Blockierende Zugriffe in <c>static void Main(...)</c>-Methoden
    /// sind erlaubt (Programm-Einstiegspunkt der vor .NET 7.1 kein async Main kannte).
    /// Standard: <c>true</c>.
    /// </summary>
    public bool BanBlockingTaskAccessAllowInMain { get; init; } = true;

    /// <summary>
    /// Wenn <c>true</c>: Blockierende Zugriffe in Testdateien werden nicht gemeldet.
    /// Nützlich für Test-Infrastruktur-Code der kein async-Setup unterstützt.
    /// Standard: <c>false</c> (Tests sollten async sein).
    /// </summary>
    public bool BanBlockingTaskAccessAllowInTests { get; init; } = false;

    /// <summary>
    /// Aktiviert die solution-weite Duplicate-Code-Erkennung (Token-CPD, Method-Granularitaet).
    /// Meldet <c>exact</c>- und <c>near</c>-Cluster als Lint-Verstoesse (<c>fuzzy</c> bleibt
    /// reine MCP-Auskunft, zu viel Rauschen fuer automatisches Lint). Standard: <c>true</c>.
    /// </summary>
    public bool EnableDuplicateCodeCheck { get; init; } = true;

    /// <summary>
    /// Mindestanzahl Tokens im Methoden-Body, ab der eine Methode ueberhaupt in die
    /// Duplicate-Code-Erkennung einfliesst. Verhindert, dass triviale Methoden (leere
    /// <c>Dispose</c>/<c>ToString</c>-Overrides) als Klone markiert werden. Standard: <c>30</c>
    /// (konservativer Start, PMD CPD nutzt 100, jscpd 50).
    /// </summary>
    public int DuplicateCodeMinTokens { get; init; } = AiNetLinter.Core.DuplicateDetection.DuplicateDetectionDefaults.MinTokens;

    /// <summary>
    /// Groesse des Sliding-Window (Token-Anzahl) fuer das N-Gram-Shingling (Broder 1997).
    /// Standard: <c>5</c>.
    /// </summary>
    public int DuplicateCodeNgramSize { get; init; } = AiNetLinter.Core.DuplicateDetection.DuplicateDetectionDefaults.NgramSize;

    /// <summary>
    /// Mindestanzahl gemeinsamer N-Gramme, ab der zwei Methoden ueberhaupt als Kandidaten-Paar
    /// fuer den exakten Jaccard-Vergleich gelten (Vorfilter ueber den Inverted Index).
    /// Standard: <c>3</c>.
    /// </summary>
    public int DuplicateCodeMinSharedNgrams { get; init; } = AiNetLinter.Core.DuplicateDetection.DuplicateDetectionDefaults.MinSharedNgrams;

    /// <summary>
    /// Jaccard-Schwellwert (inklusive), ab der ein Cluster als <c>exact</c> gilt (fast identisch,
    /// Konsolidierung dringend). Standard: <c>0.95</c>.
    /// </summary>
    public double DuplicateCodeExactThreshold { get; init; } = AiNetLinter.Core.DuplicateDetection.DuplicateDetectionDefaults.ExactThreshold;

    /// <summary>
    /// Jaccard-Schwellwert (inklusive), ab der ein Cluster als <c>near</c> gilt (sehr aehnlich,
    /// lohnt den Blick). Standard: <c>0.80</c>.
    /// </summary>
    public double DuplicateCodeNearThreshold { get; init; } = AiNetLinter.Core.DuplicateDetection.DuplicateDetectionDefaults.NearThreshold;

    /// <summary>
    /// Jaccard-Schwellwert (inklusive), ab der ein Cluster ueberhaupt noch als <c>fuzzy</c>
    /// gemeldet wird (Grenzwertig, evtl. Pattern-Klone). Unterhalb wird nichts ausgegeben.
    /// Standard: <c>0.65</c>.
    /// </summary>
    public double DuplicateCodeFuzzyThreshold { get; init; } = AiNetLinter.Core.DuplicateDetection.DuplicateDetectionDefaults.FuzzyThreshold;

    /// <summary>
    /// Wenn <c>true</c>: Identifier-Tokens werden vor dem N-Gram-Shingling auf <c>$ID$</c> und
    /// Literal-Tokens auf <c>$LIT$</c> normalisiert — schaltet Type-2-Klon-Erkennung (umbenannte
    /// Klone) an. Standard: <c>false</c> (sonst wuerden z. B. <c>CalculateOrderTotal</c> und
    /// <c>CalculateInvoiceTotal</c> faelschlich als Klon markiert).
    /// </summary>
    public bool DuplicateCodeNormalizeIdentifiers { get; init; } = AiNetLinter.Core.DuplicateDetection.DuplicateDetectionDefaults.NormalizeIdentifiers;

    /// <summary>
    /// Obergrenze der als Lint-Verstoss gemeldeten Duplicate-Code-Cluster (Top-N nach
    /// Jaccard-Score absteigend) — kein unbegrenzter Dump. Standard: <c>20</c>.
    /// </summary>
    public int DuplicateCodeMaxResults { get; init; } = AiNetLinter.Core.DuplicateDetection.DuplicateDetectionDefaults.MaxResults;
}
