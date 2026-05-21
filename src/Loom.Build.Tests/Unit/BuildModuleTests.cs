using Loom.Config;
using Loom.Modules;

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.Options;

using Moq;

namespace Loom.Build.Tests.Unit;

public class BuildModuleTests
{
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
            Global = new GlobalSettings { Target = target, Configuration = configuration },
        };
    }

    [Test]
    public async Task ExecuteAsync_PassesFixedArgumentsAndSolution()
    {
        using var tempDir = new TempDirectory();
        var settings = CreateSettings();
        var mockDotNet = new Mock<IDotNet>();

        var capturedOptions = new List<DotNetBuildOptions>();
        var capturedExecOptions = new List<CommandExecutionOptions>();



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
            .ReturnsAsync(TestHelpers.EmptyCommandResult());

        var builder = TestHelpers.CreateSilentPipelineBuilder(new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<FakeBuildMinVerModule>();
                services.AddModule<BuildModule>();
            });
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        await Assert.That(capturedOptions).Count().IsEqualTo(1);
        await Assert.That(capturedOptions[0].ProjectSolution).IsEqualTo("test.sln");
        await Assert.That(capturedOptions[0].NoRestore).IsTrue();

        await Assert.That(capturedExecOptions).Count().IsEqualTo(1);
        await Assert.That(capturedExecOptions[0].WorkingDirectory).IsEqualTo(tempDir.Path);
    }

    [Test]
    public async Task ExecuteAsync_UsesReleaseConfiguration_WhenTargetIsPublishOrRelease()
    {
        using var tempDir = new TempDirectory();
        var settings = CreateSettings(target: BuildTarget.Publish);
        var mockDotNet = new Mock<IDotNet>();

        var capturedOptions = new List<DotNetBuildOptions>();


        mockDotNet
            .Setup(d =>
                d.Build(
                    It.IsAny<DotNetBuildOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DotNetBuildOptions, CommandExecutionOptions, CancellationToken>(
                (opts, _, _) =>
                {
                    capturedOptions.Add(opts);
                }
            )
            .ReturnsAsync(TestHelpers.EmptyCommandResult());

        var builder = TestHelpers.CreateSilentPipelineBuilder(new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<FakeBuildMinVerModule>();
                services.AddModule<BuildModule>();
            });
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        await Assert.That(capturedOptions[0].Configuration).IsEqualTo("Release");
    }

    [Test]
    public async Task ExecuteAsync_UsesDebugConfiguration_WhenTargetIsDefault()
    {
        using var tempDir = new TempDirectory();
        var settings = CreateSettings(); // Defaults to Build
        var mockDotNet = new Mock<IDotNet>();

        var capturedOptions = new List<DotNetBuildOptions>();


        mockDotNet
            .Setup(d =>
                d.Build(
                    It.IsAny<DotNetBuildOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DotNetBuildOptions, CommandExecutionOptions, CancellationToken>(
                (opts, _, _) =>
                {
                    capturedOptions.Add(opts);
                }
            )
            .ReturnsAsync(TestHelpers.EmptyCommandResult());

        var builder = TestHelpers.CreateSilentPipelineBuilder(new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<FakeBuildMinVerModule>();
                services.AddModule<BuildModule>();
            });
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        await Assert.That(capturedOptions[0].Configuration).IsEqualTo("Debug");
    }

    [Test]
    public async Task ExecuteAsync_PassesVersionProperties_FromMinVer()
    {
        using var tempDir = new TempDirectory();
        var settings = CreateSettings();
        var mockDotNet = new Mock<IDotNet>();
        DotNetBuildOptions? capturedOptions = null;



        mockDotNet
            .Setup(d =>
                d.Build(
                    It.IsAny<DotNetBuildOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DotNetBuildOptions, CommandExecutionOptions, CancellationToken>((opts, _, _) =>
            {
                capturedOptions = opts;
            })
            .ReturnsAsync(TestHelpers.EmptyCommandResult());

        var builder = TestHelpers.CreateSilentPipelineBuilder(new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<FakeBuildMinVerModule>();
                services.AddModule<BuildModule>();
            });
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        await Assert.That(capturedOptions).IsNotNull();

        var properties = capturedOptions!.Properties!.ToDictionary(x => x.Key, x => x.Value);
        await Assert.That(properties["AssemblyVersion"]).IsEqualTo("1.0.0.0");
        await Assert.That(properties["FileVersion"]).IsEqualTo("1.2.3.0");
        await Assert.That(properties["InformationalVersion"]).IsEqualTo("1.2.3");
        await Assert.That(properties["PackageVersion"]).IsEqualTo("1.2.3");
        await Assert.That(properties["Version"]).IsEqualTo("1.2.3");
    }
}
