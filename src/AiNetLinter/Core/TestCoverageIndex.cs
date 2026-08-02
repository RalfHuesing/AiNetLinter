namespace AiNetLinter.Core;

public sealed class TestCoverageIndex
{
    private readonly Lock _lock = new();
    private readonly HashSet<string> _testClassNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _referencedTypeNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _coversComments = new(StringComparer.Ordinal);

    public void AddTestClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return;
        }

        lock (_lock) { _testClassNames.Add(className); }
    }

    public void AddReferencedType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return;
        }

        lock (_lock) { _referencedTypeNames.Add(typeName); }
    }

    public void AddCoversComment(string coveredTypeName)
    {
        if (string.IsNullOrWhiteSpace(coveredTypeName))
        {
            return;
        }

        lock (_lock) { _coversComments.Add(coveredTypeName); }
    }

    internal IReadOnlyCollection<string> TestClassNames { get { lock (_lock) { return [.. _testClassNames]; } } }
    internal IReadOnlyCollection<string> ReferencedTypeNames { get { lock (_lock) { return [.. _referencedTypeNames]; } } }
    internal IReadOnlyCollection<string> CoversComments { get { lock (_lock) { return [.. _coversComments]; } } }
}
