namespace SingleCompileErrorMini;

// Bewusst kaputt, damit das Workspace genau eine Datei mit Compile-Fehlern enthaelt
// und die Singular-Form der aggregierten Warnhinweis-Header-Zeile getriggert wird.
public sealed class BrokenClass
{
    public void F( { } }
