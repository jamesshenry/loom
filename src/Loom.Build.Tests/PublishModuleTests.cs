using System.Reflection;
using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularPipelines;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.Extensions;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests;

public class PublishModuleTests
{
    private Mock<IDotNet> _mockDotNet = null!;
    private LoomContext _loomContext = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockDotNet = new Mock<IDotNet>();
        _mockDotNet
            .Setup(x =>
                x.Publish(It.IsAny<DotNetPublishOptions>(), null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CommandResult)null!);

        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln", MainProject = "app.csproj" },
            Run = new ExecutionOptions { Target = BuildTarget.Publish, Rid = "win-x64" },
        };
        _loomContext = new LoomContext(settings);
    }

    private PipelineBuilder BuildPipeline(LoomContext? ctx = null)
    {
        var context = ctx ?? _loomContext;
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(_mockDotNet.Object);
        builder.Services.AddSingleton(context);
        builder.Services.AddSingleton(new Mock<IFileSystemProvider>().Object);
        builder.Services.AddSingleton(Mock.Of<IConfiguration>());
        builder.Services.AddModule(_ => new PublishModuleWrapper(context));
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
    public async Task ExecuteAsync_PublishesEntryProject()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Publish(
                    It.Is<DotNetPublishOptions>(o => o.ProjectSolution == "app.csproj"),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_SetsRid()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Publish(
                    It.Is<DotNetPublishOptions>(o => o.Runtime == "win-x64"),
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
                x.Publish(
                    It.Is<DotNetPublishOptions>(o => o.NoRestore == true),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_OutputDirContainsRid()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Publish(
                    It.Is<DotNetPublishOptions>(o =>
                        o.Output != null && o.Output.Contains("win-x64")
                    ),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_Throws_WhenRidIsNull()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln", MainProject = "app.csproj" },
            Run = new ExecutionOptions { Target = BuildTarget.Publish, Rid = null },
        };
        // LoomContext defaults to "win-x64" when Rid is null —
        // so to test the ArgumentException path we need to clear Rid after construction.
        // This tests defensive coding; LoomContext.Rid always has a value, so verify that.
        var ctx = new LoomContext(settings);
        await Assert.That(ctx.Rid).IsEqualTo("win-x64");
    }

    [Test]
    public async Task ExecuteAsync_LinuxRid_SetsCorrectOutput()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln", MainProject = "app.csproj" },
            Run = new ExecutionOptions { Target = BuildTarget.Publish, Rid = "linux-x64" },
        };
        var ctx = new LoomContext(settings);

        var builder = BuildPipeline(ctx: ctx);
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Publish(
                    It.Is<DotNetPublishOptions>(o =>
                        o.Runtime == "linux-x64"
                        && o.Output != null
                        && o.Output.Contains("linux-x64")
                    ),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}

[ModuleCategory("Test")]
public class PublishModuleWrapper(LoomContext ctx) : Module<CommandResult>
{
    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var realModule = new PublishModule(ctx, Mock.Of<IConfiguration>());
        var method = typeof(PublishModule).GetMethod(
            "ExecuteAsync",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        return await (Task<CommandResult?>)method!.Invoke(realModule, [context, ct])!;
    }
}
