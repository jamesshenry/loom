using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;
using Moq;
using File = ModularPipelines.FileSystem.File;

namespace Loom.Build.Tests.Unit;

public class NugetUploadModuleTests
{
    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static LoomSettings CreateSettings(
        bool withNugetArtifact = true,
        bool enableNugetUpload = true
    )
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                ArtifactsPath = ".artifacts",
                EnableNugetUpload = enableNugetUpload,
            },
            Run = new ExecutionOptions { Target = BuildTarget.Publish, Configuration = "Release" },
            Nuget = new NugetSettings { ApiKey = "test-api-key" },
        };

        if (withNugetArtifact)
        {
            settings.Artifacts.Add(
                "MyPackage",
                new ArtifactSettings { Type = ArtifactType.Nuget, Project = "MyPackage.csproj" }
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
        builder.Services.AddModule<NugetUploadModule>();

        // We'll mock the PackModule so NugetUploadModule has packages to upload
        var mockPackModule = new Mock<PackModule>(new LoomContext(settings, tempDir));
        builder.Services.AddSingleton(mockPackModule.Object);

        builder.Options.PrintLogo = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.PrintResults = false;
        builder.Options.PrintDependencyChains = false;
        builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Silent;

        return builder;
    }

    [Test]
    public async Task Configure_SkipsExecution_WhenNoNugetArtifactsDefined()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var settings = CreateSettings(withNugetArtifact: false);
            var mockDotNet = new Mock<IDotNet>();
            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);

            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();
            var result = await summary.GetModule<NugetUploadModule>();

            await Assert.That(result.SkipDecisionOrDefault).IsNotNull();
            await Assert.That(result.SkipDecisionOrDefault!.ShouldSkip).IsTrue();
            await Assert.That(result.SkipDecisionOrDefault.Reason).Contains("No nuget artifacts");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Configure_SkipsExecution_WhenNugetUploadDisabled()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, ".artifacts", "nuget"));
            var settings = CreateSettings(withNugetArtifact: true, enableNugetUpload: false);
            var mockDotNet = new Mock<IDotNet>();
            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);

            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();
            var result = await summary.GetModule<NugetUploadModule>();

            await Assert.That(result.SkipDecisionOrDefault).IsNotNull();
            await Assert.That(result.SkipDecisionOrDefault!.ShouldSkip).IsTrue();
            await Assert
                .That(result.SkipDecisionOrDefault.Reason)
                .Contains("disabled in workspace settings");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_PushesPackages_WhenConditionsAreMet()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, ".artifacts", "nuget"));
            var settings = CreateSettings(withNugetArtifact: true, enableNugetUpload: true);

            var capturedOptions = new List<DotNetNugetPushOptions>();

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

            var mockCommand = new Mock<ICommand>();
            var mockNuget = new Mock<DotNetNuget>(mockCommand.Object);

            mockNuget
                .Setup(n =>
                    n.Push(
                        It.IsAny<DotNetNugetPushOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<DotNetNugetPushOptions, CommandExecutionOptions, CancellationToken>(
                    (opts, _, _) => capturedOptions.Add(opts)
                )
                .ReturnsAsync(emptyCommandResult);

            mockDotNet.Setup(d => d.Nuget).Returns(mockNuget.Object);

            var builder = Pipeline.CreateBuilder();
            var loomContext = new LoomContext(settings, tempDir);

            // Simulate CI mode to avoid ctx.IsRunningLocally() skipping it
            Environment.SetEnvironmentVariable("LOOM_IGNORE_LOCAL_CHECK", "true");

            builder.Services.AddSingleton(loomContext);
            builder.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            builder.Services.AddSingleton(mockDotNet.Object);

            builder.Services.AddModule(x =>
                new FakePackModule(x.GetRequiredService<LoomContext>()) as PackModule
            );
            builder.Services.AddModule<NugetUploadModule>();
            builder.Options.PrintLogo = false;
            builder.Options.ShowProgressInConsole = false;
            builder.Options.PrintResults = false;
            builder.Options.PrintDependencyChains = false;

            var pipeline = await builder.BuildAsync();
            await pipeline.RunAsync();

            await Assert.That(capturedOptions).Count().IsEqualTo(2);
            await Assert.That(capturedOptions[0].Path).EndsWith("package1.nupkg");
            await Assert.That(capturedOptions[1].Path).EndsWith("package2.nupkg");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOOM_IGNORE_LOCAL_CHECK", null);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}

// Minimal mock to supply dependencies
public class FakePackModule : PackModule
{
    public FakePackModule(LoomContext buildContext)
        : base(buildContext) { }

    protected override async Task<PackResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        return new PackResult(
            new List<File> { new File("package1.nupkg"), new File("package2.nupkg") }
        );
    }
}
