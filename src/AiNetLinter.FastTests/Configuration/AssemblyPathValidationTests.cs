#nullable enable

using AiNetLinter.Configuration;
using Xunit;

namespace AiNetLinter.FastTests.Configuration;

[Trait("Category", "Unit")]
public sealed class AssemblyPathValidationTests
{
    [Theory]
    [InlineData("lib/Shared.dll", true)]
    [InlineData("lib/Shared.exe", true)]
    [InlineData("Shared.DLL", true)]
    [InlineData("Shared.ExE", true)]
    [InlineData("lib/Shared.txt", false)]
    [InlineData("lib/Shared.bin", false)]
    [InlineData("lib/Shared", false)]
    [InlineData("lib/Shared.dllx", false)]
    [InlineData("lib/Shared.exe.tmp", false)]
    public void IsSupportedAssemblyPath_AcceptsDllAndExeAndRejectsOtherEndings(string path, bool expected)
    {
        Assert.Equal(expected, AssemblyPathValidation.IsSupportedAssemblyPath(path));
    }

    [Theory]
    [InlineData(".dll", true)]
    [InlineData(".exe", true)]
    [InlineData(".DLL", true)]
    [InlineData(".txt", false)]
    [InlineData(".dllx", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasSupportedAssemblyExtension_AcceptsOnlyDllAndExe(string? extension, bool expected)
    {
        Assert.Equal(expected, AssemblyPathValidation.HasSupportedAssemblyExtension(extension));
    }

    [Theory]
    [InlineData("Shared.dll", "Shared")]
    [InlineData("Shared.exe", "Shared")]
    [InlineData("Shared.DLL", "Shared")]
    [InlineData("Shared.ExE", "Shared")]
    [InlineData("Shared.txt", "Shared.txt")]
    [InlineData("Shared", "Shared")]
    [InlineData("Shared.dll.x", "Shared.dll.x")]
    public void WithoutAssemblyExtension_RemovesOnlySupportedAssemblySuffix(string value, string expected)
    {
        Assert.Equal(expected, AssemblyPathValidation.WithoutAssemblyExtension(value));
    }
}