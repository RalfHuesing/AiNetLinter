#nullable enable

using System;
using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal sealed partial class LocalExternalSourceRepositoryCacheWriter
{
    private static async Task FinalizePublishAsync(
        PublishContext context,
        bool published,
        CacheKeyLockRegistry.CacheKeyLockLease? lockLease,
        Func<Task>? afterLeaseReleasedAsync)
    {
        try
        {
            if (!published)
            {
                if (context.PointerPublished)
                {
                    ExternalSourceRepositoryCacheStorage.RestorePreviousCurrent(
                        context.EntryDirectory,
                        context.GenerationName,
                        context.PreviousGeneration);
                }

                ExternalSourceRepositoryCacheStorage.TryDeleteGeneration(
                    context.EntryDirectory,
                    context.GenerationDirectory);
            }
            else
            {
                ExternalSourceRepositoryCacheStorage.RetainGenerations(
                    context.EntryDirectory,
                    context.GenerationName);
            }
        }
        finally
        {
            lockLease?.Dispose();
            await InvokeTestHookAsync(afterLeaseReleasedAsync).ConfigureAwait(false);
        }
    }
}

internal static class ExternalSourceRepositoryCachePublishLifecycle
{
    internal static async Task FinalizeAndReleaseSourceAsync(
        Func<Task> finalizeAsync,
        ExternalSourceCheckoutMaterializationUse? materializationUse)
    {
        try
        {
            await finalizeAsync().ConfigureAwait(false);
        }
        finally
        {
            materializationUse?.Dispose();
        }
    }
}
