using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularPipelines;
using ModularPipelines.Attributes;
using ModularPipelines.Configuration;
using ModularPipelines.Context;
using ModularPipelines.Enums;
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
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                MainProject = "test.csproj",
            },
            Run = new ExecutionOptions { Target = BuildTarget.Release, Rid = "win-x64" },
            Packaging = new PackagingSettings { VelopackId = "MyApp" },
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
        builder.Options.ThrowOnPipelineFailure = false; // Tests handle failures explicitly
        return builder;
    }

    [Test]
    public async Task ExecuteAsync_SkipPkg_SkipsModule()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                MainProject = "test.csproj",
            },
            Run = new ExecutionOptions { Target = BuildTarget.Release, Rid = "win-x64" },
            Packaging = new PackagingSettings { VelopackId = "MyApp" },
        };
        var ctx = new LoomContext(settings);

        var pipeline = await BuildPipeline(ctx: ctx).BuildAsync();
        var summary = await pipeline.RunAsync();

        var module = summary.GetModule<VelopackReleaseModuleWrapper>();
        var result = await module;
        await Assert.That(result.ModuleStatus).IsEqualTo(Status.Skipped);
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
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                MainProject = "test.csproj",
            },
            Packaging = new PackagingSettings { VelopackId = "MyApp" },
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
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                MainProject = "test.csproj",
            },
            Run = new ExecutionOptions { Target = BuildTarget.Release, Rid = "linux-x64" },
            Packaging = new PackagingSettings { VelopackId = "MyApp" },
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
    public async Task ExecuteAsync_Throws_WhenVelopackIdIsNull()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                MainProject = "test.csproj",
            },
            Run = new ExecutionOptions { Target = BuildTarget.Release, Rid = "win-x64" },
            Packaging = new PackagingSettings { VelopackId = "MyApp" },
        };

        var pipeline = await BuildPipeline(ctx: new LoomContext(settings)).BuildAsync();

        await Assert.ThrowsAsync<Exception>(() => pipeline.RunAsync());
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
        ArgumentException.ThrowIfNullOrWhiteSpace(ctx.Rid, nameof(ctx.Rid));
        ArgumentException.ThrowIfNullOrWhiteSpace(ctx.VelopackId, nameof(ctx.VelopackId));

        var root = context.Environment.WorkingDirectory;
        var publishDir = Path.Combine(root, "dist", "publish", ctx.Rid);
        var releaseDir = Path.Combine(root, "dist", "release", ctx.Rid);

        string directive = ctx.Rid.ToLower() switch
        {
            var r when r.StartsWith("win") => "[win]",
            var r when r.StartsWith("osx") => "[osx]",
            var r when r.StartsWith("linux") => "[linux]",
            _ => throw new NotSupportedException($"RID {ctx.Rid} is not supported."),
        };

        CapturedArguments = string.Join(
            " ",
            new[]
            {
                "vpk",
                directive,
                "pack",
                "--packId",
                ctx.VelopackId,
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
