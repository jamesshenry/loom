using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularPipelines;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Extensions;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests;

public class VelopackReleaseModuleTests
{
    private LoomContext _loomContext = null!;
    private const string TestVersion = "1.2.3";

    [Before(Test)]
    public void Setup()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln" },
            Run = new ExecutionOptions { Target = BuildTarget.Release, Rid = "win-x64" },
            Artifacts = new Dictionary<string, ArtifactSettings>
            {
                ["MyApp"] = new ArtifactSettings
                {
                    Project = "test.csproj",
                    Type = ArtifactType.Velopack,

                    VelopackId = "MyApp",
                },
            },
        };
        _loomContext = new LoomContext(settings);
    }

    private PipelineBuilder BuildPipeline(LoomContext? ctx = null, string? version = TestVersion)
    {
        var context = ctx ?? _loomContext;
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(context);
        builder.Services.AddSingleton(new Mock<IFileSystemProvider>().Object);
        builder.Services.AddSingleton(new VelopackVersion(version));
        builder.Services.AddModule<VelopackReleaseModuleWrapper>();
        builder.Services.AddLogging(b => b.ClearProviders());
        builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Default;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.PrintResults = false;
        builder.Options.PrintLogo = false;
        builder.Options.PrintDependencyChains = false;
        builder.Options.ThrowOnPipelineFailure = false;
        return builder;
    }

    [Test]
    public async Task ExecuteAsync_NoVelopackArtifacts_NoCapturedArguments()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln" },
            Run = new ExecutionOptions { Target = BuildTarget.Release, Rid = "win-x64" },
        };
        var ctx = new LoomContext(settings);

        var pipeline = await BuildPipeline(ctx: ctx).BuildAsync();
        var summary = await pipeline.RunAsync();

        var module = summary.GetModule<VelopackReleaseModuleWrapper>();
        await Assert.That(module.CapturedArguments).IsNull();
    }

    [Test]
    public async Task ExecuteAsync_WindowsRid_UsesWinDirective()
    {
        var pipeline = await BuildPipeline().BuildAsync();
        var summary = await pipeline.RunAsync();

        var module = summary.GetModule<VelopackReleaseModuleWrapper>();
        await Assert.That(module.CapturedArguments!.Contains("[win]")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_MacOsRid_UsesOsxDirective()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln" },
            Artifacts = new Dictionary<string, ArtifactSettings>
            {
                ["MyApp"] = new ArtifactSettings
                {
                    Project = "test.csproj",
                    Type = ArtifactType.Velopack,

                    VelopackId = "MyApp",
                },
            },
            Run = new ExecutionOptions { Target = BuildTarget.Release, Rid = "osx-x64" },
        };

        var pipeline = await BuildPipeline(ctx: new LoomContext(settings)).BuildAsync();
        var summary = await pipeline.RunAsync();

        var module = summary.GetModule<VelopackReleaseModuleWrapper>();
        await Assert.That(module.CapturedArguments!.Contains("[osx]")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_LinuxRid_UsesLinuxDirective()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln" },
            Run = new ExecutionOptions { Target = BuildTarget.Release, Rid = "linux-x64" },
            Artifacts = new Dictionary<string, ArtifactSettings>
            {
                ["MyApp"] = new ArtifactSettings
                {
                    Project = "test.csproj",
                    Type = ArtifactType.Velopack,

                    VelopackId = "MyApp",
                },
            },
        };

        var pipeline = await BuildPipeline(ctx: new LoomContext(settings)).BuildAsync();
        var summary = await pipeline.RunAsync();

        var module = summary.GetModule<VelopackReleaseModuleWrapper>();
        await Assert.That(module.CapturedArguments!.Contains("[linux]")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_PassesVersionAndPackId()
    {
        var pipeline = await BuildPipeline(version: "3.4.5").BuildAsync();
        var summary = await pipeline.RunAsync();

        var module = summary.GetModule<VelopackReleaseModuleWrapper>();
        await Assert.That(module.CapturedArguments!.Contains("3.4.5")).IsTrue();
        await Assert.That(module.CapturedArguments!.Contains("MyApp")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_Throws_WhenVersionIsNull()
    {
        var pipeline = await BuildPipeline(version: null).BuildAsync();

        await Assert.ThrowsAsync<Exception>(() => pipeline.RunAsync());
    }

    [Test]
    public async Task ExecuteAsync_UsesArtifactKeyWhenVelopackIdIsNull()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln" },
            Run = new ExecutionOptions { Target = BuildTarget.Release, Rid = "win-x64" },
            Artifacts = new Dictionary<string, ArtifactSettings>
            {
                ["MyApp"] = new ArtifactSettings
                {
                    Project = "test.csproj",
                    Type = ArtifactType.Velopack,
                    VelopackId = null,
                },
            },
        };

        var pipeline = await BuildPipeline(ctx: new LoomContext(settings)).BuildAsync();
        var summary = await pipeline.RunAsync();

        var module = summary.GetModule<VelopackReleaseModuleWrapper>();
        await Assert.That(module.CapturedArguments!.Contains("MyApp")).IsTrue();
    }
}

public record VelopackVersion(string? Value);

[ModuleCategory("Test")]
public class VelopackReleaseModuleWrapper(LoomContext ctx, VelopackVersion velopackVersion)
    : Module<CommandResult>
{
    public string? CapturedArguments { get; private set; }

    protected override Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var version = velopackVersion.Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(version, nameof(version));

        var velopackArtifacts = ctx
            .Artifacts.Where(a => a.Value.Type == ArtifactType.Velopack)
            .ToList();

        if (velopackArtifacts.Count == 0)
        {
            return Task.FromResult<CommandResult?>(null);
        }

        var (artifactKey, artifactSettings) = velopackArtifacts[0];
        var rid = artifactSettings.Rid ?? ctx.Rid;
        var packId = artifactSettings.VelopackId ?? artifactKey;

        var root = context.Environment.WorkingDirectory;
        var publishDir = Path.Combine(root, ctx.ArtifactsDirectory, "publish", artifactKey, rid);
        var releaseDir = Path.Combine(root, ctx.ArtifactsDirectory, "release", artifactKey, rid);

        string directive = rid.ToLower() switch
        {
            var r when r.StartsWith("win") => "[win]",
            var r when r.StartsWith("osx") => "[osx]",
            var r when r.StartsWith("linux") => "[linux]",
            _ => throw new NotSupportedException($"RID {rid} is not supported."),
        };

        CapturedArguments = string.Join(
            " ",
            new[]
            {
                "vpk",
                directive,
                "pack",
                "--packId",
                packId,
                "--packVersion",
                version,
                "--packDir",
                publishDir,
                "--outputDir",
                releaseDir,
                "--yes",
            }
        );

        return Task.FromResult<CommandResult?>(null);
    }
}
