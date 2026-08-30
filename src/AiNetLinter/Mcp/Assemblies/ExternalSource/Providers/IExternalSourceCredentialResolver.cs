#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Providers;

internal interface IExternalSourceCredentialResolver
{
    ValueTask<ExternalSourceCredential?> ResolveAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken = default);
}

internal sealed class ExternalSourceCredential : IDisposable
{
    private string? secret;

    internal ExternalSourceCredential(string username, string secret)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException(
                "Der Credential-Benutzername darf nicht leer sein.",
                nameof(username));
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException(
                "Das Credential-Geheimnis darf nicht leer sein.",
                nameof(secret));
        }

        Username = username.Trim();
        this.secret = secret;
    }

    internal string Username { get; }

    internal string Secret => secret
        ?? throw new ObjectDisposedException(nameof(ExternalSourceCredential));

    public void Dispose()
    {
        secret = null;
    }
}
