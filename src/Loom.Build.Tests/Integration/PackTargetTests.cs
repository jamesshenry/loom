using ModularPipelines.Enums;

namespace Loom.Build.Tests;

[Explicit]
[Category("Integration")]
[ClassDataSource<LoomPublishFixture>(Shared = SharedType.PerClass)]
public class PackTests(LoomPublishFixture fixture)
{
    [Test]
    public async Task Pack_ProducesNupkg()
    {
        await Assert.That(fixture.InitialSummary!.Status).IsEqualTo(Status.Successful);

        var nupkgs = Directory.GetFiles(
            Path.Combine(fixture.WorkingDir, ".artifacts", "nuget"),
            "*.nupkg"
        );
        await Assert.That(nupkgs).IsNotEmpty();
    }

    [Test]
    public async Task Pack_VersionMatchesGitTag()
    {
        var nupkgs = Directory.GetFiles(
            Path.Combine(fixture.WorkingDir, ".artifacts", "nuget"),
            "*.nupkg"
        );
        await Assert.That(nupkgs.Any(f => Path.GetFileName(f).Contains("1.2.3"))).IsTrue();
    }

    [Test]
    public async Task Pack_WithTagPrefix_VersionMatchesPrefixedTag()
    {
        var nupkgs = Directory.GetFiles(
            Path.Combine(fixture.WorkingDir, ".artifacts", "nuget"),
            "*.nupkg"
        );
        await Assert.That(nupkgs.Any(f => Path.GetFileName(f).Contains("3.0.0"))).IsTrue();
    }
}
