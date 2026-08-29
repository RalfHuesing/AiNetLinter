#nullable enable

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

/// <summary>
/// Sammelt Log-Ereignisse eines einzelnen Test-Loggers ohne den globalen
/// Serilog-Logger zu verändern.
/// </summary>
internal sealed class ExternalSourceRepositoryTestLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> events = new();

    internal LogEvent[] Events => events.ToArray();

    public void Emit(LogEvent logEvent)
    {
        events.Enqueue(logEvent);
    }
}

internal static class ExternalSourceRepositoryTestFactory
{
    internal static ExternalSourceRepositoryAcquirer CreateAcquirer(
        IGiteaRepositoryTransport transport,
        TestTempDirectory temp,
        ILogger? logger = null) =>
        new(
            transport,
            temp.DirectoryPath,
            logger,
            new LocalExternalSourceRepositoryCacheWriter(temp.DirectoryPath));
}

/// <summary>
/// Prüft vor dem echten Reparse-Test die lokale Fähigkeit für Directory-Symlinks.
/// Ein übersprungener Preflight ist kein Sicherheitsnachweis.
/// </summary>
internal static class WindowsReparseCapabilityGate
{
    internal static void Require()
    {
        using var preflight = TestTempDirectory.Create("external-source-reparse-capability-");
        var targetPath = preflight.CreateSubdirectory("target");
        var linkPath = preflight.GetPath("link");
        var linkCreated = false;
        try
        {
            try
            {
                Directory.CreateSymbolicLink(linkPath, targetPath);
                linkCreated = true;
            }
            catch (Exception exception) when (
                ExternalSourceRepositoryFailurePolicy.IsPrivilegeNotHeld(exception))
            {
                Assert.Skip(
                    "Der Testhost meldet ERROR_PRIVILEGE_NOT_HELD (1314) für "
                    + "Directory.CreateSymbolicLink. Die Symlink-Capability wurde "
                    + "nicht nachgewiesen; dieser Skip ist kein Sicherheitsnachweis. "
                    + "Der echte Reparse-Test muss privilegiert ohne Skip wiederholt werden.");
            }

            var attributes = File.GetAttributes(linkPath);
            Assert.True(
                attributes.HasFlag(FileAttributes.ReparsePoint),
                "Der Capability-Preflight hat keinen echten Directory-Reparse-Punkt erzeugt.");
        }
        finally
        {
            if (linkCreated)
            {
                Directory.Delete(linkPath);
            }
        }
    }

}

internal static class ExternalSourceRepositoryFixtureOperations
{
    internal static void CopyBaselineMiniSolution(string sourceRoot, string destination)
    {
        Directory.CreateDirectory(destination);
        File.Copy(
            Path.Combine(sourceRoot, "BaselineMini.slnx"),
            Path.Combine(destination, "BaselineMini.slnx"));
    }
}

internal static class ExternalSourceRepositoryCacheReadBackTestSupport
{
    internal static int CountGenerations(string entryDirectory) =>
        Directory.EnumerateDirectories(
                entryDirectory,
                ExternalSourceRepositoryCacheContract.GenerationDirectoryPrefix + "*",
                SearchOption.TopDirectoryOnly)
            .Count();

    internal static Func<string, Stream> CreateLengthControlledReadStream(
        string targetPath,
        byte[] contents,
        int maxBytes,
        bool oversize)
    {
        var initialLength = oversize ? (long)maxBytes + 1 : contents.Length;
        var finalLength = oversize ? initialLength : (long)contents.Length + 1;
        return path => string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase)
            ? new LengthControlledReadStream(contents, initialLength, finalLength)
            : File.OpenRead(path);
    }

    internal static byte[] CreateMalformedBytes(
        string artifact,
        string mutation,
        byte[] original,
        string generationName,
        ExternalSourceRepositoryCacheKey key,
        string solutionPath) =>
        mutation switch
        {
            "invalid-utf8" => new byte[] { 0xFF },
            "truncated" => TruncateJson(original),
            "unknown-field" => AppendRootProperty(original, "\"unexpectedField\":true"),
            "duplicate-field" => AppendRootProperty(
                original,
                artifact switch
                {
                    "current" => "\"generation\":\"" + generationName + "\"",
                    "manifest" => "\"cacheKey\":\"" + key.StableValue + "\"",
                    "inventory" => "\"cacheSchemaVersion\":\"" + key.SchemaVersion + "\"",
                    _ => throw new ArgumentException("Unbekanntes Cachemetadaten-Artefakt.", nameof(artifact)),
                }),
            "unknown-file-field" => AppendFileProperty(original, "\"unexpectedFileField\":true", solutionPath),
            "duplicate-file-field" => AppendFileProperty(
                original,
                "\"relativePath\":\"" + solutionPath + "\"",
                solutionPath),
            _ => throw new ArgumentException("Unbekannte malformed-Input-Variante.", nameof(mutation)),
        };

    internal static byte[] CreateInventoryLimitBytes(
        string mutation,
        string original,
        int fileCount) =>
        Encoding.UTF8.GetBytes(mutation switch
        {
            "entry-count" => ReplaceInventoryFiles(
                original,
                CreateInventoryEntries(
                    ExternalSourceRepositoryCacheContract.MaxInventoryEntries + 1,
                    0),
                ExternalSourceRepositoryCacheContract.MaxInventoryEntries + 1,
                0),
            "declared-total-bytes" => ReplaceTopLevelNumber(
                original,
                "totalBytes",
                (ExternalSourceRepositoryCacheContract.MaxInventoryBytes + 1)
                    .ToString(CultureInfo.InvariantCulture)),
            "cumulative-total-bytes" => ReplaceInventoryFiles(
                original,
                CreateInventoryEntries(
                    5,
                    ExternalSourceRepositoryCacheContract.MaxFileLength),
                5,
                ExternalSourceRepositoryCacheContract.MaxFileLength * 5),
            "file-length" => ReplaceFirstFileProperty(
                original,
                "length",
                (ExternalSourceRepositoryCacheContract.MaxFileLength + 1)
                    .ToString(CultureInfo.InvariantCulture)),
            "path-length" => ReplaceFirstFileProperty(
                original,
                "relativePath",
                "\""
                + new string('p', ExternalSourceRepositoryCacheContract.MaxRelativePathLength + 1)
                + "\""),
            "file-count-mismatch" => ReplaceTopLevelNumber(
                original,
                "fileCount",
                (fileCount + 1).ToString(CultureInfo.InvariantCulture)),
            _ => throw new ArgumentException("Unbekannte Inventarlimit-Variante.", nameof(mutation)),
        });

    private static string CreateInventoryEntries(int count, long length)
    {
        const string contentHash = "0000000000000000000000000000000000000000000000000000000000000000";
        var entries = new StringBuilder();
        for (var index = 0; index < count; index++)
        {
            if (index > 0)
            {
                entries.Append(',');
            }

            entries.Append("{\"relativePath\":\"limit-")
                .Append(index.ToString("D5", CultureInfo.InvariantCulture))
                .Append("\",\"length\":")
                .Append(length.ToString(CultureInfo.InvariantCulture))
                .Append(",\"contentHash\":\"")
                .Append(contentHash)
                .Append("\"}");
        }

        return entries.ToString();
    }

    private static byte[] TruncateJson(byte[] original)
    {
        var json = Encoding.UTF8.GetString(original).TrimEnd();
        Assert.EndsWith("}", json, StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(json[..^1]);
    }

    private static byte[] AppendRootProperty(byte[] original, string property)
    {
        var json = Encoding.UTF8.GetString(original);
        var objectEnd = json.LastIndexOf('}');
        Assert.True(objectEnd >= 0);
        var prefix = json[..objectEnd].TrimEnd();
        var separator = prefix.EndsWith("{", StringComparison.Ordinal) ? string.Empty : ",";
        return Encoding.UTF8.GetBytes(prefix + separator + property + "}");
    }

    private static byte[] AppendFileProperty(
        byte[] original,
        string property,
        string solutionPath)
    {
        var json = Encoding.UTF8.GetString(original);
        var relativePath = "\"relativePath\": \"" + solutionPath + "\"";
        var propertyStart = json.IndexOf(relativePath, StringComparison.Ordinal);
        Assert.True(propertyStart >= 0);
        var objectEnd = json.IndexOf('}', propertyStart + relativePath.Length);
        Assert.True(objectEnd >= 0);
        var prefix = json[..objectEnd].TrimEnd();
        return Encoding.UTF8.GetBytes(prefix + "," + property + json[objectEnd..]);
    }

    private static string ReplaceInventoryFiles(
        string original,
        string entries,
        int fileCount,
        long totalBytes)
    {
        var filesProperty = original.IndexOf("\"files\":", StringComparison.Ordinal);
        Assert.True(filesProperty >= 0);
        var arrayStart = original.IndexOf('[', filesProperty);
        var arrayEnd = original.IndexOf(']', arrayStart);
        Assert.True(arrayStart >= 0);
        Assert.True(arrayEnd >= 0);
        var json = original[..(arrayStart + 1)] + entries + original[arrayEnd..];
        json = ReplaceTopLevelNumber(
            json,
            "fileCount",
            fileCount.ToString(CultureInfo.InvariantCulture));
        return ReplaceTopLevelNumber(
            json,
            "totalBytes",
            totalBytes.ToString(CultureInfo.InvariantCulture));
    }

    private static string ReplaceTopLevelNumber(
        string json,
        string propertyName,
        string replacement)
    {
        var marker = "\"" + propertyName + "\":";
        var propertyStart = json.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(propertyStart >= 0);
        var valueStart = propertyStart + marker.Length;
        while (char.IsWhiteSpace(json[valueStart]))
        {
            valueStart++;
        }

        var valueEnd = valueStart;
        while (valueEnd < json.Length
            && (char.IsDigit(json[valueEnd]) || json[valueEnd] == '-'))
        {
            valueEnd++;
        }

        return json[..valueStart] + replacement + json[valueEnd..];
    }

    private static string ReplaceFirstFileProperty(
        string json,
        string propertyName,
        string replacement)
    {
        var filesProperty = json.IndexOf("\"files\":", StringComparison.Ordinal);
        Assert.True(filesProperty >= 0);
        var marker = "\"" + propertyName + "\":";
        var propertyStart = json.IndexOf(marker, filesProperty, StringComparison.Ordinal);
        Assert.True(propertyStart >= 0);
        var valueStart = propertyStart + marker.Length;
        while (char.IsWhiteSpace(json[valueStart]))
        {
            valueStart++;
        }

        var valueEnd = valueStart;
        if (json[valueStart] == '"')
        {
            valueEnd = json.IndexOf('"', valueStart + 1);
            Assert.True(valueEnd >= 0);
            valueEnd++;
        }
        else
        {
            while (valueEnd < json.Length
                && (char.IsDigit(json[valueEnd]) || json[valueEnd] == '-'))
            {
                valueEnd++;
            }
        }

        return json[..valueStart] + replacement + json[valueEnd..];
    }

    private sealed class LengthControlledReadStream : Stream
    {
        private readonly MemoryStream inner;
        private readonly long initialLength;
        private readonly long subsequentLength;
        private int lengthReads;

        internal LengthControlledReadStream(
            byte[] contents,
            long initialLength,
            long subsequentLength)
        {
            inner = new MemoryStream(contents, writable: false);
            this.initialLength = initialLength;
            this.subsequentLength = subsequentLength;
        }

        public override bool CanRead => true;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => Interlocked.Increment(ref lengthReads) == 1
            ? initialLength
            : subsequentLength;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
