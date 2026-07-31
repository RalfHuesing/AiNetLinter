#nullable enable

namespace SymbolGraphMini;

// Bewusste, deterministische Lint-Verletzung: fehlendes `sealed` -> EnforceSealedClasses (Default true).
public class ViolationTrigger
{
    public void DoWork()
    {
    }
}
