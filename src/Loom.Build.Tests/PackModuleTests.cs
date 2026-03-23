using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularPipelines;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.Enums;
using ModularPipelines.Extensions;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;
using Moq;
using File = ModularPipelines.FileSystem.File;

namespace Loom.Build.Tests;

public class PackModuleTests
{
    private Mock<IDotNet> _mockDotNet = null!;
    private Mock<IFileSystemProvider> _mockFileSystem = null!;
    private LoomContext _loomContext = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockDotNet = new Mock<IDotNet>();
        _mockDotNet
            .Setup(x => x.Pack(It.IsAny<DotNetPackOptions>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommandResult)null!);

        _mockFileSystem = new Mock<IFileSystemProvider>();
        _mockFileSystem
            .Setup(p => p.EnumerateFiles(It.IsAny<string>(), "*", SearchOption.TopDirectoryOnly))
            .Returns(["dist/MyPackage.1.0.0.nupkg", "dist/MyPackage.1.0.0.snupkg"]);

        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln" },
            Run = new ExecutionOptions { Target = BuildTarget.Release },
            Artifacts = new Dictionary<string, ArtifactSettings>
            {
                ["MyPackage"] = new ArtifactSettings
                {
                    Project = "test.csproj",
                    Type = ArtifactType.Nuget,
                },
            },
        };
        _loomContext = new LoomContext(settings);
    }

    private PipelineBuilder BuildPipeline(LoomContext? ctx = null)
    {
        var context = ctx ?? _loomContext;
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(_mockDotNet.Object);
        builder.Services.AddSingleton(_mockFileSystem.Object);
        builder.Services.AddSingleton(context);
        builder.Services.AddModule<PackModuleWrapper>();
        builder.Services.AddModule<MinVerMockModule>();
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
    public async Task ExecuteAsync_ReturnsPackedFiles()
    {
        var builder = BuildPipeline();
        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();

        var module = summary.GetModule<PackModuleWrapper>();
        var result = await module;
        await Assert.That(result.ValueOrDefault).IsNotNull().And.Count().IsEqualTo(2);
    }

    [Test]
    public async Task ExecuteAsync_CallsPackWithCorrectOptions()
    {
        var builder = BuildPipeline();
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Pack(
                    It.Is<DotNetPackOptions>(o =>
                        o.ProjectSolution == "test.csproj" && o.NoBuild == true
                    ),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_DoesNotThrow()
    {
        var builder = BuildPipeline();
        var pipeline = await builder.BuildAsync();

        var summary = await pipeline.RunAsync();
        await Assert.That(summary.Status).IsEqualTo(Status.Successful);
    }

    [Test]
    public async Task ExecuteAsync_PassesMinVerTagPrefix_WhenSet()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln" },
            Run = new ExecutionOptions { Target = BuildTarget.Release },
            Artifacts = new Dictionary<string, ArtifactSettings>
            {
                ["MyPackage"] = new ArtifactSettings
                {
                    Project = "test.csproj",
                    Type = ArtifactType.Nuget,
                    TagPrefix = "myapp/",
                },
            },
        };
        var ctx = new LoomContext(settings);

        var builder = BuildPipeline(ctx: ctx);
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Pack(
                    It.Is<DotNetPackOptions>(o =>
                        o.Properties != null
                        && o.Properties.Any(p => p.Key == "MinVerTagPrefix" && p.Value == "myapp/")
                    ),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_DoesNotPassMinVerTagPrefix_WhenNotSet()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Pack(
                    It.Is<DotNetPackOptions>(o => o.Properties == null),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}

[ModuleCategory("Test")]
public class MinVerMockModule : Module<string>
{
    protected override async Task<string?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        return await Task.FromResult("1.0.0");
    }
}

[ModuleCategory("Test")]
public class PackModuleWrapper(LoomContext ctx) : PackModule(ctx)
{
    protected override async Task<List<File>?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        return await base.ExecuteAsync(context, ct);
    }
}
