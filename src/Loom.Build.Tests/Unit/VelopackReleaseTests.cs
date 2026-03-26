using Loom.Modules;
using Loom.Velopack;
using Loom.Velopack.Options;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.FileSystem;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests.Unit;

public class FakePublishModule : PublishModule
{
    private readonly List<PublishedArtifact> _artifacts;

    public FakePublishModule(LoomContext loomContext, List<PublishedArtifact> artifacts)
        : base(loomContext)
    {
        _artifacts = artifacts;
    }

    protected override Task<PublishResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        return Task.FromResult<PublishResult?>(new PublishResult(_artifacts));
    }
}

public class VelopackReleaseTests
{
    private static readonly string WorkingDir = Path.Combine(Path.GetTempPath(), "loom-test");
    private const string ArtifactsDir = ".artifacts";

    private static readonly LoomContext defaultLoomContext = new()
    {
        WorkingDirectory = WorkingDir,
        Solution = "test.sln",
        ArtifactsDirectory = ArtifactsDir,
        EnableVelopack = true,
        Target = BuildTarget.Publish,
        Configuration = "Release",
        Rid = "win-x64",
    };

    private static PipelineBuilder CreateBuilder(LoomContext context)
    {
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(context);
        builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Silent;
        builder.Options.PrintLogo = false;
        return builder;
    }

    [Test]
    public async Task Configure_SkipsExecution_WhenNoVelopackArtifactsDefined()
    {
        var loomContext = defaultLoomContext with
        {
            Artifacts = new Dictionary<string, ArtifactSettings>(),
        };

        var builder = CreateBuilder(loomContext);
        builder.Services.AddModule<VelopackReleaseModule>();

        var summary = await (await builder.BuildAsync()).RunAsync();
        var result = await summary.GetModule<VelopackReleaseModule>();

        await Assert.That(result.SkipDecisionOrDefault?.ShouldSkip).IsTrue();
        await Assert.That(result.SkipDecisionOrDefault?.Reason).Contains("No velopack artifacts");
    }

    [Test]
    public async Task ExecuteAsync_CallsVPKPack_WithCorrectArguments()
    {
        var artifactName = "MyVelopackApp";
        var expectedArtifact = new ArtifactSettings
        {
            Type = ArtifactType.Velopack,
            Project = $"{artifactName}.csproj",
            VelopackId = "Custom.Id",
        };

        var loomContext = defaultLoomContext with
        {
            Artifacts = new Dictionary<string, ArtifactSettings>
            {
                [artifactName] = expectedArtifact,
            },
        };

        var packDir = Path.Combine(WorkingDir, ArtifactsDir, "publish", artifactName, "win-x64");
        var outputDir = Path.Combine(WorkingDir, ArtifactsDir, "release", artifactName, "win-x64");

        var publishedArtifacts = new List<PublishedArtifact>
        {
            new(artifactName, new Folder(packDir), "win-x64", ArtifactType.Velopack),
        };

        var mockVelopack = new Mock<IVelopackPack>();
        VelopackBaseOptions? capturedOptions = null;
        mockVelopack
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<VelopackBaseOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<VelopackBaseOptions, CommandExecutionOptions, CancellationToken>(
                (opts, _, _) => capturedOptions = opts
            )
            .Returns(Task.CompletedTask);

        var builder = CreateBuilder(loomContext);
        builder.Services.AddSingleton(mockVelopack.Object);
        builder.Services.AddModule<FakeMinVerModule>();
        builder.Services.AddModule(new FakePublishModule(loomContext, publishedArtifacts));
        builder.Services.AddModule<VelopackReleaseModule>();

        var summary = await (await builder.BuildAsync()).RunAsync();
        var moduleResult = await summary.GetModule<VelopackReleaseModule>();

        await Assert.That(moduleResult.IsSuccess).IsTrue();
        await Assert.That(capturedOptions).IsNotNull();

        // Verify properties on capturedOptions
        await Assert.That(capturedOptions!.PackId).IsEqualTo(expectedArtifact.VelopackId);
        await Assert.That(capturedOptions.PackVersion).IsEqualTo("1.2.3");
        await Assert.That(capturedOptions.PackDir).IsEqualTo(packDir);
        await Assert.That(capturedOptions.OutputDir).IsEqualTo(outputDir);
        await Assert.That(capturedOptions.Channel).IsEqualTo("win");
    }

    [Test]
    public async Task ExecuteAsync_ThrowsNotSupportedException_ForUnknownRid()
    {
        var artifactName = "MyVelopackApp";
        var loomContext = defaultLoomContext with
        {
            Rid = "unknown-rid",
            Artifacts = new Dictionary<string, ArtifactSettings>
            {
                [artifactName] = new() { Type = ArtifactType.Velopack, Project = "app.csproj" },
            },
        };
        var publishedArtifacts = new List<PublishedArtifact>
        {
            new(artifactName, new Folder(Path.GetTempPath()), "unknown-rid", ArtifactType.Velopack),
        };

        var builder = CreateBuilder(loomContext);
        builder.Services.AddModule<FakeMinVerModule>();
        builder.Services.AddModule(new FakePublishModule(loomContext, publishedArtifacts));
        builder.Services.AddModule<VelopackReleaseModule>();

        var action = async () => await (await builder.BuildAsync()).RunAsync();

        await Assert.That(action).Throws<Exception>();
    }
}
