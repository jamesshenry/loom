using Loom.Config;

namespace Loom.Build.Tests;

/// <summary>
/// Universal fixture: all projects, both version tags, all artifact types.
/// Each test class gets its own clone via SharedType.PerClass, ensuring IO isolation.
/// </summary>
public class LoomFixture : FixtureHarness
{
    protected override void ConfigureSettings(LoomSettings settings)
    {
        settings.Workspace = new WorkspaceSettings { Solution = "Fixtures.slnx" };
        settings.Artifacts = new Dictionary<string, ArtifactSettings>
        {
            ["HelloNuget"] = new ArtifactSettings
            {
                Project = "HelloNuget/HelloNuget.csproj",
                Type = ArtifactType.Nuget,
            },
            ["HelloNugetPrefixed"] = new ArtifactSettings
            {
                Project = "HelloNugetPrefixed/HelloNugetPrefixed.csproj",
                Type = ArtifactType.Nuget,
                TagPrefix = "nuget=",
            },
            ["HelloApp"] = new ArtifactSettings
            {
                Project = "HelloApp/HelloApp.csproj",
                Type = ArtifactType.Executable,
            },
        };
    }
}

public class LoomBuildFixture : LoomFixture
{
    protected override async Task RunPipelineAsync() =>
        InitialSummary = await RunAsync(BuildTarget.Build);
}

public class LoomPublishFixture : LoomFixture
{
    protected override async Task RunPipelineAsync() =>
        InitialSummary = await RunAsync(BuildTarget.Publish);
}

public class LoomTestFixture : LoomFixture
{
    protected override async Task RunPipelineAsync() =>
        InitialSummary = await RunAsync(BuildTarget.Test);
}

public class LoomCleanFixture : LoomFixture
{
    protected override async Task RunPipelineAsync()
    {
        await RunAsync(BuildTarget.Build);
        InitialSummary = await RunAsync(BuildTarget.Clean);
    }
}

/// <summary>
/// NuGet upload disabled — verifies the upload module is skipped during release.
/// </summary>
public class SkipNugetUploadFixture : FixtureHarness
{
    protected override void ConfigureSettings(LoomSettings settings)
    {
        settings.Workspace = new WorkspaceSettings
        {
            Solution = "Fixtures.slnx",
            EnableNugetUpload = false,
        };
        settings.Artifacts = new Dictionary<string, ArtifactSettings>
        {
            ["HelloNuget"] = new ArtifactSettings
            {
                Project = "HelloNuget/HelloNuget.csproj",
                Type = ArtifactType.Nuget,
            },
        };
    }

    protected override async Task RunPipelineAsync() =>
        InitialSummary = await RunAsync(BuildTarget.Release);
}

/// <summary>
/// GitHub release disabled — verifies the release module is skipped during release.
/// </summary>
public class SkipGithubReleaseFixture : FixtureHarness
{
    protected override void ConfigureSettings(LoomSettings settings)
    {
        settings.Workspace = new WorkspaceSettings
        {
            Solution = "Fixtures.slnx",
            EnableGithubRelease = false,
        };
    }

    protected override async Task RunPipelineAsync() =>
        InitialSummary = await RunAsync(BuildTarget.Release);
}
