namespace AiNetLinter.Suppression;

public static class DisableAllCommentInjector
{
    public static DisableAllInjectResult InjectIntoFiles(IReadOnlyList<string> absolutePaths)
    {
        int modified = 0;
        int skipped = 0;

        foreach (var absolutePath in absolutePaths)
        {
            if (TryInjectIntoFile(absolutePath))
            {
                modified++;
            }
            else
            {
                skipped++;
            }
        }

        return new DisableAllInjectResult(absolutePaths.Count, modified, skipped);
    }

    public static bool TryInjectIntoFile(string absolutePath)
    {
        var content = File.ReadAllText(absolutePath);
        if (SuppressionCommentParser.ContainsDisableAll(content))
        {
            return false;
        }

        File.WriteAllText(absolutePath, PrependDisableAll(content));
        return true;
    }

    internal static string PrependDisableAll(string content)
    {
        if (content.StartsWith('\uFEFF'))
        {
            return "\uFEFF" + SuppressionCommentParser.DisableAllLine + Environment.NewLine + content[1..];
        }

        return SuppressionCommentParser.DisableAllLine + Environment.NewLine + content;
    }
}
