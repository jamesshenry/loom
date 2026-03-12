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

public class TestModuleTests
{
    private Mock<IDotNet> _mockDotNet = null!;
    private LoomContext _loomContext = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockDotNet = new Mock<IDotNet>();
        _mockDotNet
            .Setup(x => x.Test(It.IsAny<DotNetTestOptions>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommandResult)null!);

        var settings = new LoomSettings
        {
            Project = new ProjectConfig { Solution = "test.sln", EntryProject = "test.csproj" },
            Build = new BuildConfig { Target = BuildTarget.Test },
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
        builder.Services.AddModule(_ => new TestModuleWrapper(context));
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
    public async Task ExecuteAsync_RunsTestsAgainstSolution()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Test(
                    It.Is<DotNetTestOptions>(o => o.Solution == "test.sln"),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_SetsNoBuild()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Test(
                    It.Is<DotNetTestOptions>(o => o.NoBuild == true),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_IncludesCoverageArguments()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Test(
                    It.Is<DotNetTestOptions>(o =>
                        o.Arguments != null
                        && o.Arguments.Contains("--coverage")
                        && o.Arguments.Contains("--coverage-output-format")
                        && o.Arguments.Contains("xml")
                    ),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_CoverageOutputPathContainsTestResults()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Test(
                    It.Is<DotNetTestOptions>(o =>
                        o.Arguments != null && o.Arguments.Any(a => a.Contains("coverage.xml"))
                    ),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_IgnoresExitCode8()
    {
        // Exit code 8 = no tests found — should not fail the pipeline
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockDotNet.Verify(
            x =>
                x.Test(
                    It.Is<DotNetTestOptions>(o =>
                        o.Arguments != null
                        && o.Arguments.Contains("--ignore-exit-code")
                        && o.Arguments.Contains("8")
                    ),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}

[ModuleCategory("Test")]
public class TestModuleWrapper(LoomContext ctx) : Module<CommandResult>
{
    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var realModule = new TestModule(ctx, Mock.Of<IConfiguration>());
        var method = typeof(TestModule).GetMethod(
            "ExecuteAsync",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        return await (Task<CommandResult?>)method!.Invoke(realModule, [context, ct])!;
    }
}
