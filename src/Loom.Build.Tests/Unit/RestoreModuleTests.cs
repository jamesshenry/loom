using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests.Unit;

public class RestoreModuleTests
{
    private static LoomSettings CreateSettings()
    {
        return new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                ArtifactsPath = ".artifacts",
            },
            Global = new GlobalSettings
            {
                Target = BuildTarget.Build,
                Configuration = "Debug",
                Rid = "win-x64",
            },
        };
    }

    [Test]
    public async Task ExecuteAsync_PassesCorrectOptionsToDotNetRestore()
    {
        const string tempDir = "/fake/workspace";
        var settings = CreateSettings();
        var mockDotNet = new Mock<IDotNet>();

        var capturedOptions = new List<DotNetRestoreOptions>();
        var capturedExecOptions = new List<CommandExecutionOptions>();

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
            .ReturnsAsync(TestHelpers.EmptyCommandResult());

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<RestoreModule>();
            }
        );
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        await Assert.That(capturedOptions).Count().IsEqualTo(1);
        await Assert.That(capturedOptions[0].ProjectSolution).IsEqualTo("test.sln");

        await Assert.That(capturedExecOptions).Count().IsEqualTo(1);
        await Assert.That(capturedExecOptions[0].WorkingDirectory).IsEqualTo(tempDir);
    }

    [Test]
    public async Task ExecuteAsync_ReturnsRestoreResult_WrappingCommandResult()
    {
        const string tempDir = "/fake/workspace";
        var settings = CreateSettings();
        var mockDotNet = new Mock<IDotNet>();
        var emptyCommandResult = TestHelpers.EmptyCommandResult();

        mockDotNet
            .Setup(d =>
                d.Restore(
                    It.IsAny<DotNetRestoreOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(emptyCommandResult);

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<RestoreModule>();
            }
        );
        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var moduleResult = await summary.GetModule<RestoreModule>();

        var val = moduleResult.ValueOrDefault;

        await Assert.That(val).IsNotNull(); // Expecting RestoreResult here
        await Assert.That(val!.CommandResult).IsNotNull();
        await Assert.That(val!.CommandResult).IsEqualTo(emptyCommandResult);
    }
}
