using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.FileSystem;
using ModularPipelines.GitHub.Options;
using ModularPipelines.Options;
using Moq;
using Octokit;

namespace Loom.Build.Tests.Unit;

public class FakeVelopackModule : VelopackReleaseModule
{
    private readonly List<VelopackArtifactResult> _results;

    public FakeVelopackModule(LoomContext loomContext, List<VelopackArtifactResult> results)
        : base(loomContext)
    {
        _results = results;
    }

    protected override Task<List<VelopackArtifactResult>?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    ) => Task.FromResult<List<VelopackArtifactResult>?>(_results);
}

public class GitHubReleaseModuleTests
{
    private static readonly string WorkingDir = Path.Combine(Path.GetTempPath(), "loom-test");

    private static readonly LoomContext defaultLoomContext = new()
    {
        WorkingDirectory = WorkingDir,
        Solution = "test.sln",
        ArtifactsDirectory = ".artifacts",
        EnableGithubRelease = true,
        Target = BuildTarget.Publish,
        Configuration = "Release",
        Rid = "win-x64",
    };

    private static PipelineBuilder CreateBuilder(LoomContext context)
    {
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(context);
        builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Silent;
        builder.Options.PrintLogo = false;
        return builder;
    }

    [Test]
    public async Task Configure_SkipsExecution_WhenGitHubReleaseDisabled()
    {
        var loomContext = defaultLoomContext with
        {
            EnableGithubRelease = false,
            Artifacts = new Dictionary<string, ArtifactSettings>
            {
                ["app"] = new() { Type = ArtifactType.Velopack, Project = "app.csproj" },
            },
        };
        Environment.SetEnvironmentVariable(
            "GITHUB_TOKEN",
            "fake-token",
            EnvironmentVariableTarget.Process
        );
        var builder = CreateBuilder(loomContext);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ModularPipelines:Secrets:GitHub:AccessToken"] =
                        Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
                }
            )
            .Build();
        builder.Services.Configure<GitHubOptions>(
            configuration.GetSection("ModularPipelines:Secrets:GitHub")
        );

        builder.Services.AddModule<FakeMinVerModule>();
        builder.Services.AddModule(new FakePublishModule(loomContext, []));
        builder.Services.AddModule<VelopackReleaseModule>();
        builder.Services.AddModule<GitHubReleaseModule>();

        var summary = await (await builder.BuildAsync()).RunAsync();
        var result = await summary.GetModule<GitHubReleaseModule>();

        await Assert.That(result.SkipDecisionOrDefault?.ShouldSkip).IsTrue();
        await Assert.That(result.SkipDecisionOrDefault?.Reason).Contains("GitHub release disabled");
    }

    [Test]
    public async Task Configure_SkipsExecution_WhenNoVelopackArtifacts()
    {
        var loomContext = defaultLoomContext with
        {
            Artifacts = new Dictionary<string, ArtifactSettings>(),
        };

        var builder = CreateBuilder(loomContext);
        builder.Services.AddModule<FakeMinVerModule>();
        builder.Services.AddModule(new FakePublishModule(loomContext, []));
        builder.Services.AddModule<VelopackReleaseModule>();
        builder.Services.AddModule<GitHubReleaseModule>();

        var summary = await (await builder.BuildAsync()).RunAsync();
        var result = await summary.GetModule<GitHubReleaseModule>();

        await Assert.That(result.SkipDecisionOrDefault?.ShouldSkip).IsTrue();
        await Assert.That(result.SkipDecisionOrDefault?.Reason).Contains("No velopack artifacts");
    }
}
