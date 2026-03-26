using Loom.Modules;
using ModularPipelines.Enums;

namespace Loom.Build.Tests;

[Explicit]
[Category("Integration")]
[ClassDataSource<LoomTestFixture>(Shared = SharedType.PerClass)]
public class TestTargetTests(LoomTestFixture fixture)
{
    [Test]
    public async Task Test_Succeeds_AndProducesCoverageXml()
    {
        await Assert.That(fixture.InitialSummary!.Status).IsEqualTo(Status.Successful);
        await Assert
            .That(File.Exists(Path.Combine(fixture.WorkingDir, "TestResults", "coverage.xml")))
            .IsTrue();
    }
}
