using Loom.Config;
using Loom.MinVer;
using Loom.Modules;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
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
            Global = new GlobalSettings { Target = target, Configuration = configuration },
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
        builder.Services.AddModule<FakeBuildMinVerModule>();
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

            var emptyCommandResult = TestHelpers.EmptyCommandResult;

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
            var emptyCommandResult = TestHelpers.EmptyCommandResult;

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
            var emptyCommandResult = TestHelpers.EmptyCommandResult;

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

    [Test]
    public async Task ExecuteAsync_PassesVersionProperties_FromMinVer()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var settings = CreateSettings();
            var mockDotNet = new Mock<IDotNet>();
            DotNetBuildOptions? capturedOptions = null;

            var emptyCommandResult = TestHelpers.EmptyCommandResult;

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
                .ReturnsAsync(emptyCommandResult);

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
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
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}

public class FakeBuildMinVerModule : MinVerModule
{
    public FakeBuildMinVerModule(LoomContext loomContext)
        : base(loomContext) { }

    protected override Task<MinVerResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    ) =>
        Task.FromResult<MinVerResult?>(
            new MinVerResult(
                new Dictionary<string, MinVerVersion>
                {
                    [string.Empty] = new MinVerVersion("1.2.3"),
                }
            )
        );
}
