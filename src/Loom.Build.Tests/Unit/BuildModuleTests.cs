using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.Models;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests.Unit;

public class BuildModuleTests
{
    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static LoomSettings CreateSettings(
        BuildTarget target = BuildTarget.Build,
        string? configuration = null
    )
    {
        return new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                ArtifactsPath = ".artifacts",
            },
            Run = new ExecutionOptions { Target = target, Configuration = configuration },
        };
    }

    private static PipelineBuilder CreateSilentPipelineBuilder(
        LoomSettings settings,
        string tempDir,
        Mock<IDotNet> mockDotNet
    )
    {
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(new LoomContext(settings, tempDir));
        builder.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        builder.Services.AddSingleton(mockDotNet.Object);
        builder.Services.AddModule<BuildModule>();

        builder.Options.PrintLogo = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.PrintResults = false;
        builder.Options.PrintDependencyChains = false;
        builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Silent;

        return builder;
    }

    [Test]
    public async Task ExecuteAsync_PassesFixedArgumentsAndSolution()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var settings = CreateSettings();
            var mockDotNet = new Mock<IDotNet>();

            var capturedOptions = new List<DotNetBuildOptions>();
            var capturedExecOptions = new List<CommandExecutionOptions>();

            var emptyCommandResult = new CommandResult(
                "",
                "",
                "",
                "",
                new Dictionary<string, string?>(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                TimeSpan.Zero,
                0
            );

            mockDotNet
                .Setup(d =>
                    d.Build(
                        It.IsAny<DotNetBuildOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<DotNetBuildOptions, CommandExecutionOptions, CancellationToken>(
                    (opts, execOpts, _) =>
                    {
                        capturedOptions.Add(opts);
                        capturedExecOptions.Add(execOpts);
                    }
                )
                .ReturnsAsync(emptyCommandResult);

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            await pipeline.RunAsync();

            await Assert.That(capturedOptions).Count().IsEqualTo(1);
            await Assert.That(capturedOptions[0].ProjectSolution).IsEqualTo("test.sln");
            await Assert.That(capturedOptions[0].NoRestore).IsTrue();

            await Assert.That(capturedExecOptions).Count().IsEqualTo(1);
            await Assert.That(capturedExecOptions[0].WorkingDirectory).IsEqualTo(tempDir);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_UsesReleaseConfiguration_WhenTargetIsPublishOrRelease()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var settings = CreateSettings(target: BuildTarget.Publish);
            var mockDotNet = new Mock<IDotNet>();

            var capturedOptions = new List<DotNetBuildOptions>();
            var emptyCommandResult = new CommandResult(
                "",
                "",
                "",
                "",
                new Dictionary<string, string?>(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                TimeSpan.Zero,
                0
            );

            mockDotNet
                .Setup(d =>
                    d.Build(
                        It.IsAny<DotNetBuildOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<DotNetBuildOptions, CommandExecutionOptions, CancellationToken>(
                    (opts, execOpts, _) =>
                    {
                        capturedOptions.Add(opts);
                    }
                )
                .ReturnsAsync(emptyCommandResult);

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            await pipeline.RunAsync();

            await Assert.That(capturedOptions[0].Configuration).IsEqualTo("Release");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_UsesDebugConfiguration_WhenTargetIsDefault()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var settings = CreateSettings(); // Defaults to Build
            var mockDotNet = new Mock<IDotNet>();

            var capturedOptions = new List<DotNetBuildOptions>();
            var emptyCommandResult = new CommandResult(
                "",
                "",
                "",
                "",
                new Dictionary<string, string?>(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                TimeSpan.Zero,
                0
            );

            mockDotNet
                .Setup(d =>
                    d.Build(
                        It.IsAny<DotNetBuildOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<DotNetBuildOptions, CommandExecutionOptions, CancellationToken>(
                    (opts, execOpts, _) =>
                    {
                        capturedOptions.Add(opts);
                    }
                )
                .ReturnsAsync(emptyCommandResult);

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            await pipeline.RunAsync();

            await Assert.That(capturedOptions[0].Configuration).IsEqualTo("Debug");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
