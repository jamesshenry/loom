using Loom.Modules;
using Microsoft.Extensions.DependencyInjection;
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

    [Test]
    public async Task Configure_SkipsExecution_WhenNoNugetArtifactsDefined()
    {
        const string tempDir = "/fake/workspace";
        var mockDotNet = new Mock<IDotNet>();
        var builder = TestHelpers.CreateSilentPipelineBuilder(
            _loomContext with
            {
                WorkingDirectory = tempDir,
            },
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<FakeBuildModule>();
                services.AddModule<FakeMinVerModule>();
                services.AddModule<PackModule>();
            }
        );
        builder.AddMockFileSystem();

        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var result = await summary.GetModule<PackModule>();

        await Assert.That(result.SkipDecisionOrDefault).IsNotNull();
        await Assert.That(result.SkipDecisionOrDefault!.ShouldSkip).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_IteratesAndPacksAllNugetArtifacts_WithCorrectVersionFromMinVer()
    {
        const string tempDir = "/fake/workspace";
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
                (options, _, _) => capturedOptions.Add(options)
            )
            .ReturnsAsync(TestHelpers.EmptyCommandResult());

        List<string> expectedFiles =
        [
            Path.Combine(tempDir, ".artifacts", "nuget", "package1.1.2.3.nupkg"),
            Path.Combine(tempDir, ".artifacts", "nuget", "package2.1.2.3.nupkg"),
        ];

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            context,
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<FakeBuildModule>();
                services.AddModule<FakeMinVerModule>();
                services.AddModule<PackModule>();
            }
        );
        var mockFs = builder.AddMockFileSystem();
        mockFs
            .Setup(p => p.EnumerateFiles(It.IsAny<string>(), "*", SearchOption.TopDirectoryOnly))
            .Returns(expectedFiles);

        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();

        var moduleResult = await summary.GetModule<PackModule>();

        await Assert.That(moduleResult.IsSuccess).IsTrue();
        await Assert.That(capturedOptions.Count).IsEqualTo(2);

        foreach (var options in capturedOptions)
        {
            var properties = options.Properties!.ToDictionary(p => p.Key, p => p.Value);
            await Assert.That(properties["AssemblyVersion"]).IsEqualTo("1.0.0.0");
            await Assert.That(properties["FileVersion"]).IsEqualTo("1.2.3.0");
            await Assert.That(properties["InformationalVersion"]).IsEqualTo("1.2.3");
            await Assert.That(properties["PackageVersion"]).IsEqualTo("1.2.3");
            await Assert.That(properties["Version"]).IsEqualTo("1.2.3");
        }

        var packResult = moduleResult.ValueOrDefault!;
        await Assert
            .That(packResult.Artifacts.Select(a => a.OriginalPath))
            .IsEquivalentTo(expectedFiles);
    }

    [Test]
    public async Task ExecuteAsync_UsesPrefixVersion_WhenMatches()
    {
        const string tempDir = "/fake/workspace";
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
                (o, _, _) => captured = o
            )
            .ReturnsAsync(TestHelpers.EmptyCommandResult());

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            context,
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<FakeBuildModule>();
                services.AddModule<FakeMinVerModule>();
                services.AddModule<PackModule>();
            }
        );
        var mockFs = builder.AddMockFileSystem();
        mockFs
            .Setup(p => p.EnumerateFiles(It.IsAny<string>(), "*", SearchOption.TopDirectoryOnly))
            .Returns([Path.Combine(tempDir, ".artifacts", "nuget", "prefixed.1.2.4.nupkg")]);
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        await Assert.That(captured).IsNotNull();
        var properties = captured!.Properties!.ToDictionary(p => p.Key, p => p.Value);
        await Assert.That(properties["AssemblyVersion"]).IsEqualTo("1.0.0.0");
        await Assert.That(properties["FileVersion"]).IsEqualTo("1.2.4.0");
        await Assert.That(properties["InformationalVersion"]).IsEqualTo("1.2.4");
        await Assert.That(properties["PackageVersion"]).IsEqualTo("1.2.4");
        var version = properties["Version"];
        // FakeMinVerModule returns "1.2.4" for prefix "v"
        await Assert.That(version).IsEqualTo("1.2.4");
    }
}
