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

public class RestoreModuleTests
{
    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static LoomSettings CreateSettings()
    {
        return new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                ArtifactsPath = ".artifacts",
            },
            Run = new ExecutionOptions
            {
                Target = BuildTarget.Build,
                Configuration = "Debug",
                Rid = "win-x64",
            },
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
        builder.Services.AddModule<RestoreModule>();

        builder.Options.PrintLogo = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.PrintResults = false;
        builder.Options.PrintDependencyChains = false;
        builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Silent;

        return builder;
    }

    [Test]
    public async Task ExecuteAsync_PassesCorrectOptionsToDotNetRestore()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var settings = CreateSettings();
            var mockDotNet = new Mock<IDotNet>();

            var capturedOptions = new List<DotNetRestoreOptions>();
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
                    d.Restore(
                        It.IsAny<DotNetRestoreOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<DotNetRestoreOptions, CommandExecutionOptions, CancellationToken>(
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
            await Assert.That(capturedOptions[0].Runtime).IsEqualTo("win-x64");

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
    public async Task ExecuteAsync_ReturnsRestoreResult_WrappingCommandResult()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var settings = CreateSettings();
            var mockDotNet = new Mock<IDotNet>();
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
                    d.Restore(
                        It.IsAny<DotNetRestoreOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(emptyCommandResult);

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();
            var moduleResult = await summary.GetModule<RestoreModule>();

            var val = moduleResult.ValueOrDefault;

            await Assert.That(val).IsNotNull(); // Expecting RestoreResult here
            await Assert.That(val!.CommandResult).IsNotNull();
            await Assert.That(val!.CommandResult).IsEqualTo(emptyCommandResult);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
