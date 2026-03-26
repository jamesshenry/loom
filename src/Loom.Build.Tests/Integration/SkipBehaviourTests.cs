using Loom.Modules;

namespace Loom.Build.Tests;

[Explicit]
[Category("Integration")]
[ClassDataSource<SkipNugetUploadFixture>(Shared = SharedType.PerClass)]
public class SkipNugetUploadTests(SkipNugetUploadFixture fixture)
{
    [Test]
    public async Task NugetUpload_Skipped_WhenEnableNugetUploadIsFalse()
    {
        var nugetModuleResult = (
            await fixture.InitialSummary!.GetModuleResultsAsync()
        ).SingleOrDefault(mr => mr.ModuleName == nameof(NugetUploadModule))!;

        await Assert.That(nugetModuleResult.IsSkipped).IsTrue();
    }
}

[Explicit]
[Category("Integration")]
[ClassDataSource<SkipGithubReleaseFixture>(Shared = SharedType.PerClass)]
public class SkipGithubReleaseTests(SkipGithubReleaseFixture fixture)
{
    [Test]
    public async Task GitHubRelease_Skipped_WhenEnableGithubReleaseIsFalse()
    {
        var githubModuleResult = (
            await fixture.InitialSummary!.GetModuleResultsAsync()
        ).SingleOrDefault(mr => mr.ModuleName == nameof(GitHubReleaseModule))!;

        await Assert.That(githubModuleResult.IsSkipped).IsTrue();
    }
}
