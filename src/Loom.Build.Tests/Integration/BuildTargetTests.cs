using ModularPipelines.Enums;

namespace Loom.Build.Tests;

[Explicit]
[Category("Integration")]
[ClassDataSource<LoomBuildFixture>(Shared = SharedType.PerClass)]
public class BuildTargetTests(LoomBuildFixture fixture)
{
    [Test]
    public async Task Build_Succeeds()
    {
        await Assert.That(fixture.InitialSummary!.Status).IsEqualTo(Status.Successful);
    }
}
