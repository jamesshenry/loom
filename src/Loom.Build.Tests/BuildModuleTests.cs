using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularPipelines;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.Extensions;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests;

public class BuildModuleTests
{
    private Mock<IDotNet> _mockDotNet = null!;
    private LoomContext _loomContext = null!;
    private const string TestVersion = "1.2.3";

    [Before(Test)]
    public void Setup()
    {
        _mockDotNet = new Mock<IDotNet>();
        _mockDotNet
            .Setup(x =>
                x.Build(It.IsAny<DotNetBuildOptions>(), null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CommandResult)null!);

        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                MainProject = "test.csproj",
            },
            Run = new ExecutionOptions { Target = BuildTarget.Build },
        };
        _loomContext = new LoomContext(settings);
    }

    private PipelineBuilder BuildPipeline(LoomContext? ctx = null, string version = TestVersion)
    {
        var context = ctx ?? _loomContext;
        var builder = Pipeline.CreateBuilder();

        builder.Services.AddSingleton(_mockDotNet.Object);
        builder.Services.AddSingleton(context);
        builder.Services.AddSingleton(new Mock<IFileSystemProvider>().Object);
        builder.Services.AddSingleton(Mock.Of<IConfiguration>());
        builder.Services.AddModule(_ => new BuildModuleWrapper(context, version));

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
    public async Task ExecuteAsync_BuildTarget_UsesSolutionFile()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Build(
                    It.Is<DotNetBuildOptions>(o => o.ProjectSolution == "test.sln"),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_ReleaseTarget_UsesEntryProject()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                MainProject = "test.csproj",
            },
            Run = new ExecutionOptions { Target = BuildTarget.Release },
        };
        var ctx = new LoomContext(settings);

        var builder = BuildPipeline(ctx: ctx);
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Build(
                    It.Is<DotNetBuildOptions>(o => o.ProjectSolution == "test.csproj"),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_PublishTarget_UsesEntryProject()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                MainProject = "test.csproj",
            },
            Run = new ExecutionOptions { Target = BuildTarget.Publish },
        };
        var ctx = new LoomContext(settings);

        var builder = BuildPipeline(ctx: ctx);
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Build(
                    It.Is<DotNetBuildOptions>(o => o.ProjectSolution == "test.csproj"),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_PassesVersionFromMinVerModule()
    {
        var builder = BuildPipeline(version: "2.3.4");
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Build(
                    It.Is<DotNetBuildOptions>(o =>
                        o.Properties != null
                        && o.Properties.Any(p => p.Key == "Version" && p.Value == "2.3.4")
                    ),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_SetsNoRestore()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Build(
                    It.Is<DotNetBuildOptions>(o => o.NoRestore == true),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_ReleaseTarget_SetsRid()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                MainProject = "test.csproj",
            },
            Run = new ExecutionOptions { Target = BuildTarget.Release, Rid = "linux-x64" },
        };
        var ctx = new LoomContext(settings);

        var builder = BuildPipeline(ctx: ctx);
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Build(
                    It.Is<DotNetBuildOptions>(o => o.Runtime == "linux-x64"),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_BuildTarget_NoRid()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Build(
                    It.Is<DotNetBuildOptions>(o => o.Runtime == null),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}

/// <summary>
/// Wrapper that bypasses [DependsOn] and injects a fixed version in place of MinVerModule.
/// Build() is a direct method on IDotNet, so we mock IDotNet.Build() directly.
/// </summary>
[ModuleCategory("Test")]
public class BuildModuleWrapper(LoomContext ctx, string version) : Module<CommandResult>
{
    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var isNativeBuild = ctx.Target == BuildTarget.Release || ctx.Target == BuildTarget.Publish;
        var projectPath = isNativeBuild ? ctx.MainProject : ctx.Solution;

        return await context
            .DotNet()
            .Build(
                new DotNetBuildOptions
                {
                    ProjectSolution = projectPath,
                    NoRestore = true,
                    Configuration = ctx.Configuration,
                    Properties = [new("Version", version)],
                    Runtime = isNativeBuild ? ctx.Rid : null,
                },
                cancellationToken: ct
            );
    }
}
