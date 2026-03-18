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

public class RestoreModuleTests
{
    private Mock<IDotNet> _mockDotNet = null!;
    private LoomContext _loomContext = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockDotNet = new Mock<IDotNet>();
        _mockDotNet
            .Setup(x =>
                x.Restore(
                    It.IsAny<DotNetRestoreOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((CommandResult)null!);

        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln" },
            Run = new ExecutionOptions { Target = BuildTarget.Build },
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
        builder.Services.AddModule(_ => new RestoreModuleWrapper(context));
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
    public async Task ExecuteAsync_RestoresSolutionFile()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Restore(
                    It.Is<DotNetRestoreOptions>(o => o.ProjectSolution == "test.sln"),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_PassesRidToRestore()
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln" },
            Run = new ExecutionOptions { Target = BuildTarget.Release, Rid = "linux-x64" },
        };
        var ctx = new LoomContext(settings);

        var builder = BuildPipeline(ctx: ctx);
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Restore(
                    It.Is<DotNetRestoreOptions>(o => o.Runtime == "linux-x64"),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_UsesThrowOnNonZeroExitCode()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Restore(
                    It.IsAny<DotNetRestoreOptions>(),
                    It.Is<CommandExecutionOptions>(o => o.ThrowOnNonZeroExitCode == true),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}

[ModuleCategory("Test")]
public class RestoreModuleWrapper(LoomContext ctx) : Module<CommandResult>
{
    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var realModule = new RestoreModule(ctx, Mock.Of<IConfiguration>());
        var method = typeof(RestoreModule).GetMethod(
            "ExecuteAsync",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        return await (Task<CommandResult?>)method!.Invoke(realModule, [context, ct])!;
    }
}
