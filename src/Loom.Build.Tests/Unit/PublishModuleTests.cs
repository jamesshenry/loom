using Loom.Config;
using Loom.MinVer;
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

public class PublishModuleTests
{
    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static LoomContext CreateTestContext(bool withPublishableArtifact = true, string tempDir = "/test")
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln", ArtifactsPath = ".artifacts" },
            Global = new GlobalSettings
            {
                Target = BuildTarget.Publish,
                Rid = "linux-x64", // The Global Default
                Configuration = "Release",
            },
        };

        var resolved = new List<ResolvedArtifact>();

        if (withPublishableArtifact)
        {
            // Artifact 1: Explicit RID overrides Global
            var myApp = new ArtifactSettings { Type = ArtifactType.Executable, Project = "MyApp.csproj", Rid = "win-x64" };
            settings.Artifacts.Add("MyApp", myApp);
            resolved.Add(new ResolvedArtifact("MyApp", myApp, "win-x64", IsAot: false, CanBuildOnHost: true));

            // Artifact 2: No RID, falls back to Global "linux-x64"
            var myVelo = new ArtifactSettings
            {
                Type = ArtifactType.Velopack,
                Project = "MyVelopack.csproj",
                TagPrefix = "v",
            };
            settings.Artifacts.Add("MyVelopack", myVelo);
            resolved.Add(new ResolvedArtifact("MyVelopack", myVelo, "linux-x64", IsAot: false, CanBuildOnHost: true));
        }
        else
        {
            var myPackage = new ArtifactSettings { Type = ArtifactType.Nuget, Project = "MyLib.csproj" };
            settings.Artifacts.Add("MyPackage", myPackage);
            resolved.Add(new ResolvedArtifact("MyPackage", myPackage, "linux-x64", IsAot: false, CanBuildOnHost: true));
        }

        // Return the context with the "Reality" (ResolvedArtifacts) populated
        return new LoomContext(settings, tempDir)
        {
            ResolvedArtifacts = resolved.AsReadOnly()
        };
    }

    private static PipelineBuilder CreateSilentPipelineBuilder(
        LoomContext context,
        Mock<IDotNet> mockDotNet
    )
    {
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(context);
        builder.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        builder.Services.AddSingleton(mockDotNet.Object);
        builder.Services.AddModule<FakePublishMinVerModule>();
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
            var context = CreateTestContext(false, CreateTemporaryDirectory());
            var mockDotNet = new Mock<IDotNet>();
            var builder = CreateSilentPipelineBuilder(context, mockDotNet);

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

            var context = CreateTestContext(true, tempDir);
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

            var builder = CreateSilentPipelineBuilder(context, mockDotNet);
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
            var context = CreateTestContext(true, tempDir);
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

            var builder = CreateSilentPipelineBuilder(context, mockDotNet);
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

            var appOptions = capturedOptions.First(x => x.ProjectSolution == "MyApp.csproj");
            var veloOptions = capturedOptions.First(x => x.ProjectSolution == "MyVelopack.csproj");

            var appProperties = appOptions.Properties!.ToDictionary(x => x.Key, x => x.Value);
            var veloProperties = veloOptions.Properties!.ToDictionary(x => x.Key, x => x.Value);

            await Assert.That(appProperties["AssemblyVersion"]).IsEqualTo("1.0.0.0");
            await Assert.That(appProperties["FileVersion"]).IsEqualTo("1.2.3.0");
            await Assert.That(appProperties["InformationalVersion"]).IsEqualTo("1.2.3");
            await Assert.That(appProperties["PackageVersion"]).IsEqualTo("1.2.3");
            await Assert.That(appProperties["Version"]).IsEqualTo("1.2.3");

            await Assert.That(veloProperties["AssemblyVersion"]).IsEqualTo("1.0.0.0");
            await Assert.That(veloProperties["FileVersion"]).IsEqualTo("1.2.4.0");
            await Assert.That(veloProperties["InformationalVersion"]).IsEqualTo("1.2.4");
            await Assert.That(veloProperties["PackageVersion"]).IsEqualTo("1.2.4");
            await Assert.That(veloProperties["Version"]).IsEqualTo("1.2.4");
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
            var context = CreateTestContext(true, tempDir);
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

            var builder = CreateSilentPipelineBuilder(context, mockDotNet);
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

public class FakePublishMinVerModule : MinVerModule
{
    public FakePublishMinVerModule(LoomContext loomContext)
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
                    ["v"] = new MinVerVersion("1.2.4"),
                }
            )
        );
}
