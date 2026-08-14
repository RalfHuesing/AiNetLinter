#nullable enable

using System.Reflection;
using AiNetLinter.Baseline;
using Xunit;

namespace AiNetLinter.FastTests.Baseline;

[Trait("Category", "Unit")]
public sealed class SourceFileCatalogRegistrationPolicyTests
{
    [Fact]
    public void RegisterMSBuild_HasStaticLockField_ForThreadSafeRegistration()
    {
        // Struktureller Kern-Nachweis: nach dem Fix MUSS ein privates statisches
        // Lock-Objekt auf SourceFileCatalogLoader existieren, das die Race-Bedingung in
        // RegisterMSBuild serialisiert.
        var lockField = typeof(SourceFileCatalogLoader).GetField(
            "MsBuildRegistrationLock",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(lockField);
        Assert.Equal(typeof(object), lockField!.FieldType);
        Assert.True(lockField.IsStatic);
    }
}
