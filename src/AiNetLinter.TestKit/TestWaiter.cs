#nullable enable

using System;
using System.Threading.Tasks;

namespace AiNetLinter.TestKit;

public static class TestWaiter
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

    public static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(condition);
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(PollInterval).ConfigureAwait(false);
        }

        if (!condition()) throw new TimeoutException("Bedingung wurde nicht innerhalb des Zeitlimits erfuellt.");
    }
}
