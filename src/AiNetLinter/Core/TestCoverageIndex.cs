namespace AiNetLinter.Core;

/// <summary>
/// Sammelt Testabdeckungssignale aus Testprojekten für den Static Test Sentinel.
/// </summary>
public sealed class TestCoverageIndex
{
    private readonly HashSet<string> _testClassNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _referencedTypeNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _coversComments = new(StringComparer.Ordinal);

    /// <summary>
    /// Registriert eine Testklasse mit mindestens einer Testmethode.
    /// </summary>
    public void AddTestClass(string className) => _testClassNames.Add(className);

    /// <summary>
    /// Registriert einen per typeof/nameof referenzierten Typnamen aus einem Test.
    /// </summary>
    public void AddReferencedType(string typeName) => _referencedTypeNames.Add(typeName);

    /// <summary>
    /// Registriert einen @covers-Kommentar aus einer Testdatei.
    /// </summary>
    public void AddCoversComment(string coveredTypeName) => _coversComments.Add(coveredTypeName);

    internal IReadOnlyCollection<string> TestClassNames => _testClassNames;
    internal IReadOnlyCollection<string> ReferencedTypeNames => _referencedTypeNames;
    internal IReadOnlyCollection<string> CoversComments => _coversComments;
}
