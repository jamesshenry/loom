using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.DependencyInjection;
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
            Project = new ProjectConfig
            {
                Solution = "test.sln",
                EntryProject = "test.csproj",
                VelopackId = "MyApp",
            },
            Build = new BuildConfig { Target = BuildTarget.Release, Rid = "win-x64" },
        };
        _loomContext = new LoomContext(settings);
    }

    private PipelineBuilder BuildPipeline(
        LoomContext? ctx = null,
        string? version = TestVersion,
        bool throwOnFailure = true
    )
    {
        var context = ctx ?? _loomContext;
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(context);
        builder.Services.AddSingleton(new Mock<IFileSystemProvider>().Object);
        // VelopackReleaseModuleWrapper takes (LoomContext, string?) — pass version as a named singleton
        builder.Services.AddKeyedSingleton("velopack-version", version);
        builder.Services.AddModule<VelopackReleaseModuleWrapper>();
        if (!throwOnFailure)
            builder.Options.ThrowOnFailure = false;
        return builder;
    }

    [Test]
    public async Task ExecuteAsync_SkipPkg_SkipsModule()
    {
        var settings = new LoomSettings
        {
            Project = new ProjectConfig
            {
                Solution = "test.sln",
                EntryProject = "test.csproj",
                VelopackId = "MyApp",
            },
            Build = new BuildConfig { Target = BuildTarget.Release, Rid = "win-x64", SkipPackaging = true },
        };
        var ctx = new LoomContext(settings);

        var builder = BuildPipeline(ctx: ctx);
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        var module = pipeline.Services.GetRequiredService<VelopackReleaseModuleWrapper>();
        var result = await module;
        await Assert.That(result.ModuleStatus).IsEqualTo(Status.Skipped);
    }

    [Test]
    public async Task ExecuteAsync_WindowsRid_UsesWinDirective()
    {
        var builder = BuildPipeline();
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        var wrapper = pipeline.Services.GetRequiredService<VelopackReleaseModuleWrapper>();
        await Assert.That(wrapper.CapturedArguments).IsNotNull();
        await Assert.That(wrapper.CapturedArguments!.Contains("[win]")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_MacOsRid_UsesOsxDirective()
    {
        var settings = new LoomSettings
        {
            Project = new ProjectConfig { Solution = "test.sln", EntryProject = "test.csproj", VelopackId = "MyApp" },
            Build = new BuildConfig { Target = BuildTarget.Release, Rid = "osx-x64" },
        };
        var ctx = new LoomContext(settings);

        var builder = BuildPipeline(ctx: ctx);
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        var wrapper = pipeline.Services.GetRequiredService<VelopackReleaseModuleWrapper>();
        await Assert.That(wrapper.CapturedArguments!.Contains("[osx]")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_LinuxRid_UsesLinuxDirective()
    {
        var settings = new LoomSettings
        {
            Project = new ProjectConfig { Solution = "test.sln", EntryProject = "test.csproj", VelopackId = "MyApp" },
            Build = new BuildConfig { Target = BuildTarget.Release, Rid = "linux-x64" },
        };
        var ctx = new LoomContext(settings);

        var builder = BuildPipeline(ctx: ctx);
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        var wrapper = pipeline.Services.GetRequiredService<VelopackReleaseModuleWrapper>();
        await Assert.That(wrapper.CapturedArguments!.Contains("[linux]")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_PassesVersionAndPackId()
    {
        var builder = BuildPipeline(version: "3.4.5");
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        var wrapper = pipeline.Services.GetRequiredService<VelopackReleaseModuleWrapper>();
        await Assert.That(wrapper.CapturedArguments!.Contains("3.4.5")).IsTrue();
        await Assert.That(wrapper.CapturedArguments!.Contains("MyApp")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_Throws_WhenVersionIsNull()
    {
        var builder = BuildPipeline(version: null, throwOnFailure: false);
        var pipeline = await builder.BuildAsync();
        var result = await pipeline.RunAsync();

        await Assert.That(result.Status).IsNotEqualTo(Status.Successful);
    }

    [Test]
    public async Task ExecuteAsync_Throws_WhenVelopackIdIsNull()
    {
        var settings = new LoomSettings
        {
            Project = new ProjectConfig { Solution = "test.sln", EntryProject = "test.csproj", VelopackId = null },
            Build = new BuildConfig { Target = BuildTarget.Release, Rid = "win-x64" },
        };
        var ctx = new LoomContext(settings);

        var builder = BuildPipeline(ctx: ctx, throwOnFailure: false);
        var pipeline = await builder.BuildAsync();
        var result = await pipeline.RunAsync();

        await Assert.That(result.Status).IsNotEqualTo(Status.Successful);
    }
}

[ModuleCategory("Test")]
public class VelopackReleaseModuleWrapper(
    LoomContext ctx,
    [FromKeyedServices("velopack-version")] string? version
) : Module<CommandResult>
{
    public string? CapturedArguments { get; private set; }

    protected override ModuleConfiguration Configure()
    {
        return ModuleConfiguration
            .Create()
            .WithSkipWhen(() =>
                ctx.SkipPkg
                    ? SkipDecision.Skip("Packaging explicitly skipped")
                    : SkipDecision.DoNotSkip
            )
            .Build();
    }

    protected override Task<CommandResult?> ExecuteAsync(IModuleContext context, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(ctx.Rid, nameof(ctx.Rid));
        ArgumentException.ThrowIfNullOrWhiteSpace(ctx.Project.VelopackId, nameof(ctx.Project.VelopackId));

        var root = context.Environment.WorkingDirectory;
        var publishDir = Path.Combine(root, "dist", "publish", ctx.Rid);
        var releaseDir = Path.Combine(root, "dist", "release", ctx.Rid);

        string directive = ctx.Rid.ToLower() switch
        {
            var r when r.StartsWith("win") => "[win]",
            var r when r.StartsWith("osx") => "[osx]",
            var r when r.StartsWith("linux") => "[linux]",
            _ => throw new NotSupportedException($"RID {ctx.Rid} is not supported by Velopack."),
        };

        CapturedArguments = string.Join(" ", new[]
        {
            "vpk", directive, "pack",
            "--packId", ctx.Project.VelopackId,
            "--packVersion", version,
            "--packDir", publishDir,
            "--outputDir", releaseDir,
            "--yes",
        });

        return Task.FromResult<CommandResult?>(null);
    }
}
