#nullable enable

using Xunit;

namespace AiNetLinter.Tests.Fixtures;

// Eine geteilte SymbolGraphCatalogFixture-Instanz pro Collection; reduziert 18
// unabhaengige MSBuildWorkspace-Loads derselben Mini-Solution auf einen.
[CollectionDefinition("SymbolGraphCatalog")]
public sealed class SymbolGraphCatalogCollection : ICollectionFixture<SymbolGraphCatalogFixture>
{
}
