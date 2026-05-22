using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests.Unit;

public class CleanModuleTests
{
    private static LoomSettings CreateSettings(string?[]? additionalCleanDirectories = null)
    {
        return new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                ArtifactsPath = ".artifacts",
                CleanDirectories = (additionalCleanDirectories ?? [])
                    .Where(x => x is not null)
                    .Cast<string>()
                    .ToArray(),
            },
            Global = new GlobalSettings { Target = BuildTarget.Clean },
        };
    }

    [Test]
    public async Task ExecuteAsync_WhenDirectoryExists_DeletesDirectoryAndComputesBytesDeleted()
    {
        const string tempDir = "/fake/workspace";
        var rawArtifactsPath = Path.Combine(tempDir, ".artifacts");
        var artifactsPath = Path.GetFullPath(Path.Combine(tempDir, ".artifacts"));

        var settings = CreateSettings();
        var mockDotNet = new Mock<IDotNet>();

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<CleanModule>();
            }
        );
        var mockFs = builder.AddMockFileSystem();
        mockFs.Setup(x => x.DirectoryExists(rawArtifactsPath)).Returns(true);
        mockFs.Setup(x => x.DirectoryExists(artifactsPath)).Returns(true);
        mockFs
            .Setup(x => x.EnumerateFiles(rawArtifactsPath, "*", SearchOption.AllDirectories))
            .Returns([Path.Combine(rawArtifactsPath, "dummy.txt")]);
        mockFs
            .Setup(x => x.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3, 4, 5]);

        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var result = await summary.GetModule<CleanModule>();

        var cleanResult = result.ValueOrDefault;

        await Assert.That(cleanResult).IsNotNull();
        await Assert.That(cleanResult!.Success).IsTrue();
        await Assert.That(cleanResult.DirectoryExisted).IsTrue();
        await Assert.That(cleanResult.BytesDeleted).IsEqualTo(5L);
        mockFs.Verify(x => x.DeleteDirectory(artifactsPath, true), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WhenDirectoryDoesNotExist_ReturnsSuccessAndExistedFalse()
    {
        const string tempDir = "/fake/workspace";
        var settings = CreateSettings();
        var mockDotNet = new Mock<IDotNet>();

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<CleanModule>();
            }
        );
        builder.AddMockFileSystem();

        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var result = await summary.GetModule<CleanModule>();

        var cleanResult = result.ValueOrDefault;

        await Assert.That(cleanResult).IsNotNull();
        await Assert.That(cleanResult!.Success).IsTrue();
        await Assert.That(cleanResult.DirectoryExisted).IsFalse();
        await Assert.That(cleanResult.BytesDeleted).IsNull();
    }

    [Test]
    public async Task ExecuteAsync_ExecutesDotNetClean_AgainstWorkspaceSolution()
    {
        const string tempDir = "/fake/workspace";
        var settings = CreateSettings();
        var mockDotNet = new Mock<IDotNet>();
        DotNetCleanOptions? capturedOptions = null;

        mockDotNet
            .Setup(x =>
                x.Clean(
                    It.IsAny<DotNetCleanOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DotNetCleanOptions, CommandExecutionOptions, CancellationToken>(
                (options, _, _) => capturedOptions = options
            )
            .ReturnsAsync((CommandResult)null!);

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<CleanModule>();
            }
        );
        builder.AddMockFileSystem();

        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var result = await summary.GetModule<CleanModule>();

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(capturedOptions).IsNotNull();
        await Assert.That(capturedOptions!.ProjectSolution).IsEqualTo("test.sln");
    }

    [Test]
    public async Task ExecuteAsync_WithAdditionalCleanDirectories_DeletesAllConfiguredDirectories()
    {
        const string tempDir = "/fake/workspace";
        var rawArtifactsPath = Path.Combine(tempDir, ".artifacts");
        var artifactsPath = Path.GetFullPath(Path.Combine(tempDir, ".artifacts"));
        var nodeModulesPath = Path.GetFullPath(Path.Combine(tempDir, "node_modules"));
        var testResultsPath = Path.GetFullPath(Path.Combine(tempDir, "TestResults"));

        var settings = CreateSettings(["node_modules", "TestResults"]);
        var mockDotNet = new Mock<IDotNet>();

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<CleanModule>();
            }
        );
        var mockFs = builder.AddMockFileSystem();
        mockFs.Setup(x => x.DirectoryExists(rawArtifactsPath)).Returns(true);
        mockFs.Setup(x => x.DirectoryExists(artifactsPath)).Returns(true);
        mockFs.Setup(x => x.DirectoryExists(nodeModulesPath)).Returns(true);
        mockFs.Setup(x => x.DirectoryExists(testResultsPath)).Returns(true);

        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var result = await summary.GetModule<CleanModule>();

        await Assert.That(result.IsSuccess).IsTrue();
        mockFs.Verify(x => x.DeleteDirectory(artifactsPath, true), Times.Once);
        mockFs.Verify(x => x.DeleteDirectory(nodeModulesPath, true), Times.Once);
        mockFs.Verify(x => x.DeleteDirectory(testResultsPath, true), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithOverlappingCleanDirectories_HandlesGracefullyWithoutThrowing()
    {
        const string tempDir = "/fake/workspace";
        var rawArtifactsPath = Path.Combine(tempDir, ".artifacts");
        var artifactsPath = Path.GetFullPath(Path.Combine(tempDir, ".artifacts"));
        var innerPath = Path.GetFullPath(Path.Combine(artifactsPath, "nested"));

        var settings = CreateSettings([".artifacts/nested", ".artifacts"]); // Exact overlap and child path
        var mockDotNet = new Mock<IDotNet>();

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<CleanModule>();
            }
        );
        var mockFs = builder.AddMockFileSystem();
        mockFs.Setup(x => x.DirectoryExists(rawArtifactsPath)).Returns(true);
        mockFs.Setup(x => x.DirectoryExists(artifactsPath)).Returns(true);
        mockFs.Setup(x => x.DirectoryExists(innerPath)).Returns(true);

        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var result = await summary.GetModule<CleanModule>();

        await Assert.That(result.IsSuccess).IsTrue();
        mockFs.Verify(x => x.DeleteDirectory(artifactsPath, true), Times.Once);
        mockFs.Verify(x => x.DeleteDirectory(innerPath, true), Times.Once);
    }
}
