using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.Models;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests.Unit;

public class PublishModuleTests
{
    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static LoomSettings CreateSettings(bool withPublishableArtifact = true)
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                ArtifactsPath = ".artifacts",
            },
            Global = new GlobalSettings
            {
                Target = BuildTarget.Publish,
                Rid = "linux-x64",
                Configuration = "Release",
            },
        };

        if (withPublishableArtifact)
        {
            settings.Artifacts.Add(
                "MyApp",
                new ArtifactSettings
                {
                    Type = ArtifactType.Executable,
                    Project = "MyApp.csproj",
                    Rid = "win-x64", // Explicit rid overlay
                }
            );

            settings.Artifacts.Add(
                "MyVelopack",
                new ArtifactSettings
                {
                    Type = ArtifactType.Velopack,
                    Project = "MyVelopack.csproj",
                    // No explicit rid, should fallback to Context setting
                }
            );
        }
        else
        {
            settings.Artifacts.Add(
                "MyPackage",
                new ArtifactSettings { Type = ArtifactType.Nuget, Project = "MyLib.csproj" }
            );
        }

        return settings;
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
        builder.Services.AddModule<PublishModule>();

        builder.Options.PrintLogo = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.PrintResults = false;
        builder.Options.PrintDependencyChains = false;
        builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Silent;

        return builder;
    }

    [Test]
    public async Task Configure_SkipsExecution_WhenNoPublishableArtifactsDefined()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var settings = CreateSettings(withPublishableArtifact: false);
            var mockDotNet = new Mock<IDotNet>();
            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);

            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();
            var result = await summary.GetModule<PublishModule>();

            await Assert.That(result.SkipDecisionOrDefault).IsNotNull();
            await Assert.That(result.SkipDecisionOrDefault!.ShouldSkip).IsTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_DeletesExistingPublishDirectory_BeforePublishing()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var publishDir = Path.Combine(tempDir, ".artifacts", "publish", "MyApp", "win-x64");
            Directory.CreateDirectory(publishDir);
            var dummyFile = Path.Combine(publishDir, "old-binary.dll");
            await File.WriteAllTextAsync(dummyFile, "dummy");

            var settings = CreateSettings();
            var mockDotNet = new Mock<IDotNet>();
            mockDotNet
                .Setup(x =>
                    x.Publish(
                        It.IsAny<DotNetPublishOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((CommandResult)null!);

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            await pipeline.RunAsync();

            await Assert.That(File.Exists(dummyFile)).IsFalse();
            // Publish will recreate the folder theoretically, but the initial delete should clear old files natively.
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_ResolvesRid_FromArtifactSettingsFirstThenContext()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var settings = CreateSettings();
            var mockDotNet = new Mock<IDotNet>();
            var capturedOptions = new List<DotNetPublishOptions>();

            mockDotNet
                .Setup(x =>
                    x.Publish(
                        It.IsAny<DotNetPublishOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<DotNetPublishOptions, CommandExecutionOptions, CancellationToken>(
                    (options, _, _) => capturedOptions.Add(options)
                )
                .ReturnsAsync((CommandResult)null!);

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            await pipeline.RunAsync();

            await Assert.That(capturedOptions).Count().IsEqualTo(2);

            // MyApp specified win-x64
            await Assert
                .That(
                    capturedOptions.Any(x =>
                        x.Runtime == "win-x64" && x.ProjectSolution == "MyApp.csproj"
                    )
                )
                .IsTrue();

            // MyVelopack had no RID, falls back to context linux-x64
            await Assert
                .That(
                    capturedOptions.Any(x =>
                        x.Runtime == "linux-x64" && x.ProjectSolution == "MyVelopack.csproj"
                    )
                )
                .IsTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_ReturnsPublishResult_WrappingPublishedArtifacts()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var settings = CreateSettings();
            var mockDotNet = new Mock<IDotNet>();

            mockDotNet
                .Setup(x =>
                    x.Publish(
                        It.IsAny<DotNetPublishOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((CommandResult)null!);

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();
            var moduleResult = await summary.GetModule<PublishModule>();

            var result = moduleResult.ValueOrDefault;

            await Assert.That(result).IsNotNull(); // Expecting PublishResult here
            await Assert.That(result!.Artifacts).Count().IsEqualTo(2);
            await Assert
                .That(result.Artifacts.Any(x => x.ArtifactName == "MyApp" && x.Rid == "win-x64"))
                .IsTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
