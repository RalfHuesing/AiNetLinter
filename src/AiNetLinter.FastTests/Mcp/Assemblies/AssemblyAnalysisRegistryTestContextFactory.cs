#nullable enable

using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

internal static class AssemblyAnalysisRegistryTestContextFactory
{
    internal static async Task<AssemblyContext> CreateAsync(Solution solution)
    {
        var project = solution.Projects.Single();
        var compilation = (await project.GetCompilationAsync())!;
        return new AssemblyContext(
            compilation.Assembly,
            new AssemblyIdentityDto("EntryTest", "1.0.0.0", "", ""),
            [],
            [],
            compilation,
            null,
            null,
            new AssemblyOrigin("test", "entry-test.dll", "test-hash", "", "high"),
            1,
            AssemblySessionStatus.Complete);
    }
}
