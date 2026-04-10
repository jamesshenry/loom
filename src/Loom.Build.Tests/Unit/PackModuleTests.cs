using Loom.MinVer;
using Loom.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests.Unit;

public class PackModuleTests
{
    private readonly LoomContext _loomContext = new()
    {
        Solution = "test.slnx",
        WorkingDirectory = "/test/working/directory",
    };

    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static PipelineBuilder CreateSilentPipelineBuilder(
        LoomContext context,
        string tempDir,
        Mock<IDotNet> mockDotNet,
        Mock<IFileSystemProvider>? mockProvider = null
    )
    {
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(context);
        builder.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        builder.Services.AddSingleton(mockDotNet.Object);
        if (mockProvider?.Object is not null)
        {
            builder.Services.AddSingleton(mockProvider.Object);
        }
        builder.Services.AddModule<FakeBuildModule>();
        builder.Services.AddModule<FakeMinVerModule>();
        builder.Services.AddModule<PackModule>();

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
            var mockDotNet = new Mock<IDotNet>();
            var builder = CreateSilentPipelineBuilder(_loomContext, tempDir, mockDotNet);

            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();
            var result = await summary.GetModule<PackModule>();

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
    public async Task ExecuteAsync_IteratesAndPacksAllNugetArtifacts_WithCorrectVersionFromMinVer()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var mockDotNet = new Mock<IDotNet>();
            var context = _loomContext with
            {
                WorkingDirectory = tempDir,
                Artifacts = new Dictionary<string, ArtifactSettings>
                {
                    ["package1"] = new() { Type = ArtifactType.Nuget, Project = "package1.csproj" },
                    ["package2"] = new() { Type = ArtifactType.Nuget, Project = "package2.csproj" },
                },
            };

            var capturedOptions = new List<DotNetPackOptions>();

            mockDotNet
                .Setup(x =>
                    x.Pack(
                        It.IsAny<DotNetPackOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<DotNetPackOptions, CommandExecutionOptions, CancellationToken>(
                    (options, _, _) =>
                    {
                        capturedOptions.Add(options);
                        if (options.Output != null)
                        {
                            Directory.CreateDirectory(options.Output);
                        }
                    }
                )
                .ReturnsAsync(
                    new CommandResult(
                        "",
                        "",
                        "",
                        "",
                        new Dictionary<string, string?>(),
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow,
                        TimeSpan.Zero,
                        0
                    )
                );

            List<string> expectedFiles =
            [
                Path.Combine(tempDir, ".artifacts", "nuget", "package1.1.2.3.nupkg"),
                Path.Combine(tempDir, ".artifacts", "nuget", "package2.1.2.3.nupkg"),
            ];

            var mockProvider = new Mock<IFileSystemProvider>();
            mockProvider
                .Setup(p =>
                    p.EnumerateFiles(It.IsAny<string>(), "*", SearchOption.TopDirectoryOnly)
                )
                .Returns(expectedFiles);

            var builder = CreateSilentPipelineBuilder(context, tempDir, mockDotNet, mockProvider);

            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();

            var moduleResult = await summary.GetModule<PackModule>();

            await Assert.That(moduleResult.IsSuccess).IsTrue();
            await Assert.That(capturedOptions.Count).IsEqualTo(2);

            foreach (var options in capturedOptions)
            {
                var version = options.Properties!.First(p => p.Key == "Version").Value;
                await Assert.That(version).IsEqualTo("1.2.3");
            }

            var packResult = moduleResult.ValueOrDefault!;
            await Assert
                .That(packResult.Artifacts.Select(a => a.OriginalPath))
                .IsEquivalentTo(expectedFiles);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_UsesPrefixVersion_WhenMatches()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var mockDotNet = new Mock<IDotNet>();
            var context = _loomContext with
            {
                WorkingDirectory = tempDir,
                Artifacts = new Dictionary<string, ArtifactSettings>
                {
                    ["prefixed"] = new()
                    {
                        Type = ArtifactType.Nuget,
                        Project = "prefixed.csproj",
                        TagPrefix = "v",
                    },
                },
            };

            DotNetPackOptions? captured = null;
            mockDotNet
                .Setup(x =>
                    x.Pack(
                        It.IsAny<DotNetPackOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<DotNetPackOptions, CommandExecutionOptions, CancellationToken>(
                    (o, _, _) =>
                    {
                        captured = o;
                        if (o.Output != null)
                            Directory.CreateDirectory(o.Output);
                    }
                )
                .ReturnsAsync(
                    new CommandResult(
                        "",
                        "",
                        "",
                        "",
                        new Dictionary<string, string?>(),
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow,
                        TimeSpan.Zero,
                        0
                    )
                );

            var builder = CreateSilentPipelineBuilder(context, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            await pipeline.RunAsync();

            await Assert.That(captured).IsNotNull();
            var version = captured!.Properties!.First(p => p.Key == "Version").Value;
            // FakeMinVerModule returns "1.2.4" for prefix "v"
            await Assert.That(version).IsEqualTo("1.2.4");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}

public class FakeBuildModule : BuildModule
{
    public FakeBuildModule(LoomContext loomContext)
        : base(loomContext) { }

    protected override Task<BuildResult?> ExecuteAsync(IModuleContext context, CancellationToken ct)
    {
        return Task.FromResult<BuildResult?>(new BuildResult("success"));
    }
}

public class FakeMinVerModule : MinVerModule
{
    public static readonly MinVerVersion MinVer123 = new("1.2.3");
    public static readonly MinVerVersion MinVer124 = new("1.2.4");

    public FakeMinVerModule(LoomContext loomContext)
        : base(loomContext) { }

    protected override Task<MinVerResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    ) =>
        Task.FromResult<MinVerResult?>(
            new MinVerResult(
                new Dictionary<string, MinVerVersion>
                {
                    [string.Empty] = MinVer123,
                    ["v"] = MinVer124,
                }
            )
        );
}
