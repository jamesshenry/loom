using Loom.Modules;
using ModularPipelines.Enums;

namespace Loom.Build.Tests;

[Explicit]
[Category("Integration")]
[ClassDataSource<LoomCleanFixture>(Shared = SharedType.PerClass)]
public class CleanTargetTests(LoomCleanFixture fixture)
{
    [Test]
    public async Task Clean_RemovesArtifactsDirectory()
    {
        var moduleResults = await fixture.InitialSummary!.GetModuleResultsAsync();
        var moduleResult = moduleResults.Single(r => r.ModuleName == nameof(CleanModule));
        var result = (moduleResult.ValueOrDefault as CleanResult)!;

        await Assert.That(moduleResult.IsSuccess).IsTrue();
        await Assert.That(result.Success).IsTrue();
    }
}
