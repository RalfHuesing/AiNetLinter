Wir haben die Readme.md die viele informationen für LLMs beinhaltet.
Ich hätte gerne eine --command-line option die mir die readme ausgibt.
die readme muss also irgendwie auch fest in der .exe (oder so stecken)
in dem usage müsste was stehen das die readme N-byte groß ist damit das llm nicht von hunderten zeilen überrascht wird.

a) was hälst du von der idee?
b) wie könnte man das umsetzen?

---

## a) Was ich von der Idee halte

Die Idee ist **großartig und absolut konsequent** für ein Tool, das sich "CLI-Linter für AI-optimierten .NET Code" nennt.

Aus Sicht der Human-Developer-Erfahrung (DX) ist es zwar nett, eine README parat zu haben, aber für autonome **AI-Agenten (LLM-Loops)** ist diese Option ein mächtiges Werkzeug. Wenn ein Agent die Codebase analysiert und auf ein unbekanntes Tool wie den `AiNetLinter` stößt, kann er über `ainetlinter --readme` sofort seine "Bedienungsanleitung" und die architektonischen Leitplanken einlesen, ohne das Dateisystem nach Dokumenten durchwühlen zu müssen.

Dass du im Usage-Text direkt die **Byte-Größe** beziehungsweise den ungefähren Kontext-Footprint angeben willst, ist genial. Das schützt den Agenten vor "Context Blinding" (unerwartet riesigen Token-Dumps) und erlaubt es ihm, die Anfrage taktisch zu planen.

---

## b) Wie man das umsetzen könnte

Da das Projekt mit `TargetFramework net10.0` arbeitet, können wir das Datei-Embedding extrem elegant über die integrierten MSBuild-Features und ein schlankes API-Streaming lösen.

Hier ist der Schritt-für-Schritt-Plan für deine Codebase:

### 1. Projektdatei anpassen (`AiNetLinter.csproj`)

Zuerst sorgen wir dafür, dass die `README.md` beim Kompilieren als Ressource direkt in die Assembly eingebettet wird. Füge diesen Block in deine `.csproj` ein:

```xml
  <ItemGroup>
    <EmbeddedResource Include="README.md" LogicalName="README.md" Condition="Exists('README.md')" />
  </ItemGroup>

```

### 2. CLI-Option bereitstellen (`Cli\CliOptionFactory.cs`)

Wir erweitern die Factory um die neue Option. Da wir die Datei einbetten, können wir zur Laufzeit *während des Anwendungsstarts* die exakte Byte-Größe aus dem Ressourcen-Stream auslesen und dynamisch in die Hilfe-Beschreibung packen!

```csharp
internal static class CliOptionFactory
{
    // ... bestehende Optionen ...

    internal static Option<bool> CreateReadmeOption()
    {
        long byteCount = 0;
        try
        {
            using var stream = typeof(CliOptionFactory).Assembly.GetManifestResourceStream("README.md");
            if (stream != null) byteCount = stream.Length;
        }
        catch { /* Fallback auf 0 bei Fehlern */ }

        return new Option<bool>("--readme")
        {
            Description = $"Gibt die integrierte README.md fuer KI-Agenten aus (Achtung: Kontext-Footprint ca. {byteCount} Bytes).",
        };
    }
}

```

### 3. Parser erweitern (`Cli\CliCommandBuilder.cs`)

Jetzt hängen wir die Option in das Command-System ein:

```csharp
internal static class CliCommandBuilder
{
    internal sealed record Options(
        // ... bestehende Optionen ...
        Option<bool> Readme // HIER ZUFÜGEN
    );

    internal sealed record ParsedArgs(
        // ... bestehende Argumente ...
        bool Readme // HIER ZUFÜGEN
    );

    internal static (RootCommand Root, Options Options) Build()
    {
        var options = CreateOptions();
        var root = new RootCommand("AiNetLinter - CLI-Linter für AI-optimierten .NET Code")
        {
            // ... bestehende Optionen ...
            options.Readme // HIER ZUFÜGEN
        };

        return (root, options);
    }

    private static Options CreateOptions()
    {
        return new Options(
            // ... bestehende Aufrufe ...
            CliOptionFactory.CreateReadmeOption() // HIER ZUFÜGEN
        );
    }

    internal static ParsedArgs Parse(ParseResult parseResult, Options options)
    {
        return new ParsedArgs(
            // ... bestehende Aufrufe ...
            parseResult.GetValue(options.Readme) // HIER ZUFÜGEN
        );
    }
}

```

### 4. Argumenten-Container updaten (`Cli\LinterArgs.cs`)

Wir reichen die Eigenschaft an das interne Argumenten-Objekt weiter:

```csharp
public sealed class LinterArgs
{
    // ... bestehende Properties ...

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob die eingebettete README ausgegeben werden soll.
    /// </summary>
    public bool Readme { get; init; }
}

```

### 5. Logik im Einstiegspunkt verankern (`Program.cs`)

Zuletzt mappen wir den Parameter in `Program.cs` und fangen ihn direkt am Anfang von `ExecuteLinterAsync` ab, um die README auszugeben und die Anwendung sofort erfolgreich zu beenden:

```csharp
public static class Program
{
    // ...

    private static LinterArgs ToLinterArgs(CliCommandBuilder.ParsedArgs parsed)
    {
        return new LinterArgs
        {
            // ... bestehende Mappings ...
            Readme = parsed.Readme, // HIER ZUFÜGEN
        };
    }

    private static async Task<int> ExecuteLinterAsync(LinterArgs args)
    {
        // Falls --readme aufgerufen wurde, sofort ausgeben und beenden
        if (args.Readme)
        {
            return RunPrintReadme();
        }

        var validationError = ValidateArgs(args);
        // ... Rest der bestehenden Logik
    }

    private static int RunPrintReadme()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("README.md");
        if (stream == null)
        {
            Console.Error.WriteLine("[ERROR]: Die README.md wurde nicht als eingebettete Ressource gefunden.");
            return 1;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        Console.WriteLine(reader.ReadToEnd());
        return 0;
    }
}

```
