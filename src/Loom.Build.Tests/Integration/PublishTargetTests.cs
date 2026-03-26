using ModularPipelines.Enums;

namespace Loom.Build.Tests;

[Explicit]
[Category("Integration")]
[ClassDataSource<LoomPublishFixture>(Shared = SharedType.PerClass)]
public class PublishTargetTests(LoomPublishFixture fixture)
{
    [Test]
    public async Task Publish_ProducesBinaryInArtifactsDir()
    {
        await Assert.That(fixture.InitialSummary!.Status).IsEqualTo(Status.Successful);

        var publishDirs = Directory.GetDirectories(
            Path.Combine(fixture.WorkingDir, ".artifacts", "publish", "HelloApp"),
            "*",
            SearchOption.TopDirectoryOnly
        );
        await Assert.That(publishDirs).IsNotEmpty();

        var anyBinary = publishDirs
            .SelectMany(d => Directory.GetFiles(d))
            .Any(f =>
                f.EndsWith("HelloApp", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith("HelloApp.exe", StringComparison.OrdinalIgnoreCase)
            );
        await Assert.That(anyBinary).IsTrue();
    }
}
