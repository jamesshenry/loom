using Loom.Config;
using Loom.Modules;

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
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
        using var tempDir = new TempDirectory();
        var artifactsPath = Path.Combine(tempDir, ".artifacts");
        Directory.CreateDirectory(artifactsPath);
        var dummyFile = Path.Combine(artifactsPath, "dummy.txt");
        await File.WriteAllTextAsync(dummyFile, "12345"); // 5 bytes

        var settings = CreateSettings();
        var mockDotNet = new Mock<IDotNet>();

        var builder = TestHelpers.CreateSilentPipelineBuilder(new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<CleanModule>();
            });

        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var result = await summary.GetModule<CleanModule>();

        var cleanResult = result.ValueOrDefault;

        await Assert.That(cleanResult).IsNotNull();
        await Assert.That(cleanResult!.Success).IsTrue();
        await Assert.That(cleanResult.DirectoryExisted).IsTrue();
        await Assert.That(cleanResult.BytesDeleted).IsEqualTo(5L);
        await Assert.That(Directory.Exists(artifactsPath)).IsFalse();
    }

    [Test]
    public async Task ExecuteAsync_WhenDirectoryDoesNotExist_ReturnsSuccessAndExistedFalse()
    {
        using var tempDir = new TempDirectory();
        var settings = CreateSettings();
        var mockDotNet = new Mock<IDotNet>();

        var builder = TestHelpers.CreateSilentPipelineBuilder(new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<CleanModule>();
            });

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
        using var tempDir = new TempDirectory();
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

        var builder = TestHelpers.CreateSilentPipelineBuilder(new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<CleanModule>();
            });

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
        using var tempDir = new TempDirectory();
        var artifactsPath = Path.Combine(tempDir, ".artifacts");
        Directory.CreateDirectory(artifactsPath);

        var nodeModulesPath = Path.Combine(tempDir, "node_modules");
        Directory.CreateDirectory(nodeModulesPath);

        var testResultsPath = Path.Combine(tempDir, "TestResults");
        Directory.CreateDirectory(testResultsPath);

        var settings = CreateSettings(["node_modules", "TestResults"]);
        var mockDotNet = new Mock<IDotNet>();

        var builder = TestHelpers.CreateSilentPipelineBuilder(new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<CleanModule>();
            });

        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var result = await summary.GetModule<CleanModule>();

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(Directory.Exists(artifactsPath)).IsFalse();
        await Assert.That(Directory.Exists(nodeModulesPath)).IsFalse();
        await Assert.That(Directory.Exists(testResultsPath)).IsFalse();
    }

    [Test]
    public async Task ExecuteAsync_WithOverlappingCleanDirectories_HandlesGracefullyWithoutThrowing()
    {
        using var tempDir = new TempDirectory();
        var artifactsPath = Path.Combine(tempDir, ".artifacts");
        Directory.CreateDirectory(artifactsPath);

        var innerPath = Path.Combine(artifactsPath, "nested");
        Directory.CreateDirectory(innerPath);

        var settings = CreateSettings([".artifacts/nested", ".artifacts"]); // Exact overlap and child path
        var mockDotNet = new Mock<IDotNet>();

        var builder = TestHelpers.CreateSilentPipelineBuilder(new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<CleanModule>();
            });

        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var result = await summary.GetModule<CleanModule>();

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(Directory.Exists(artifactsPath)).IsFalse();
    }
}
