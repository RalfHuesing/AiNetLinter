#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AiNetLinter.Core.Git;

/// <summary>
/// Führt Git-Diff-Befehle aus und parst Hunks und Zeilenspannen aus der Diff-Ausgabe —
/// fokussierter interner Helper für <see cref="DiffImpactAnalyzer"/>.
/// </summary>
internal static class GitDiffParser
{
    private const string GitCommand = "git";
    private const string FilePathPrefix = "+++ b/";
    private const string HunkPrefix = "@@ ";

    internal static string? RunGitDiff(string repoRoot, string? gitSinceRef)
    {
        if (string.IsNullOrEmpty(gitSinceRef))
        {
            var (headExit, headStdout, _) = RunGitProcess(repoRoot, "diff -U0 HEAD -- *.cs");
            if (headExit == 0) return headStdout;

            var (_, unstaged, _) = RunGitProcess(repoRoot, "diff -U0 -- *.cs");
            var (_, cached, _) = RunGitProcess(repoRoot, "diff -U0 --cached -- *.cs");
            var combined = (unstaged ?? "") + "\n" + (cached ?? "");
            return string.IsNullOrWhiteSpace(combined) ? null : combined;
        }

        var (exitCode, stdout, stderr) = RunGitProcess(repoRoot, $"diff -U0 {gitSinceRef} -- *.cs");
        if (exitCode == 0) return stdout;

        throw new GitDiffFailedException(gitSinceRef, stderr.Trim());
    }

    internal static string? RunGitUntrackedFiles(string repoRoot)
    {
        var (exitCode, stdout, _) = RunGitProcess(repoRoot, "ls-files --others --exclude-standard -- *.cs");
        return exitCode == 0 ? stdout : null;
    }

    internal static (int ExitCode, string Stdout, string Stderr) RunGitProcess(string repoRoot, string args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = GitCommand,
                Arguments = args,
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null) return (-1, string.Empty, string.Empty);

            process.StandardInput.Close();

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.Append(e.Data).Append('\n'); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.Append(e.Data).Append('\n'); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();
            return (process.ExitCode, stdout.ToString(), stderr.ToString());
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }

    /// <summary>
    /// Eine Parse-Wahrheit: die bestehende Zeilen-Expansion wird aus den kompakten
    /// <see cref="HunkRange"/>s abgeleitet, damit Range- und Zeilen-Sicht nicht auseinanderdriften.
    /// </summary>
    internal static Dictionary<string, List<int>> ParseGitDiffHunks(string gitDiffOutput)
    {
        var result = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in ParseGitDiffHunkRanges(gitDiffOutput))
        {
            result[pair.Key] = ExpandHunkRanges(pair.Value);
        }
        return result;
    }

    internal static Dictionary<string, List<HunkRange>> ParseGitDiffHunkRanges(string gitDiffOutput)
    {
        var result = new Dictionary<string, List<HunkRange>>(StringComparer.OrdinalIgnoreCase);
        var lines = gitDiffOutput.Split('\n');
        string? currentFile = null;

        foreach (var line in lines)
        {
            currentFile = ProcessDiffLine(line, currentFile, result);
        }

        return result;
    }

    private static string? ProcessDiffLine(string line, string? currentFile, Dictionary<string, List<HunkRange>> result)
    {
        if (line.StartsWith(FilePathPrefix, StringComparison.Ordinal))
        {
            return line.Substring(FilePathPrefix.Length).Trim().Replace('/', Path.DirectorySeparatorChar);
        }

        if (currentFile != null && line.StartsWith(HunkPrefix, StringComparison.Ordinal))
        {
            ParseHunkLine(line, currentFile, result);
        }

        return currentFile;
    }

    private static void ParseHunkLine(string line, string currentFile, Dictionary<string, List<HunkRange>> result)
    {
        if (!TryExtractHunkRange(line, out var startLine, out var count))
        {
            return;
        }

        if (!result.TryGetValue(currentFile, out var ranges))
        {
            ranges = [];
            result[currentFile] = ranges;
        }

        ranges.Add(new HunkRange(startLine, count));
    }

    private static bool TryExtractHunkRange(string line, out int startLine, out int count)
    {
        startLine = 0;
        count = 0;

        var parts = line.Split(' ');
        if (parts.Length < 3) return false;

        var plusPart = parts[2];
        if (!plusPart.StartsWith('+')) return false;

        var numbers = plusPart.Substring(1).Split(',');
        if (!int.TryParse(numbers[0], out startLine)) return false;

        count = 1;
        if (numbers.Length > 1)
        {
            _ = int.TryParse(numbers[1], out count);
        }

        return true;
    }

    internal static List<int> ExpandHunkRanges(IReadOnlyList<HunkRange> ranges)
    {
        var lines = new List<int>();
        foreach (var range in ranges)
        {
            for (var i = 0; i < range.LineCount; i++)
            {
                lines.Add(range.StartLine + i);
            }
        }
        return lines;
    }

    internal static List<ChangedFileRange> BuildChangedFiles(Dictionary<string, List<HunkRange>> hunkRanges) =>
        hunkRanges.Select(pair => new ChangedFileRange(pair.Key, pair.Value)).ToList();
}
