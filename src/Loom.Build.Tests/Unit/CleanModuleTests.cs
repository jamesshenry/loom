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

public class CleanModuleTests
{
    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

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
            Run = new ExecutionOptions { Target = BuildTarget.Clean },
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
        builder.Services.AddModule<CleanModule>();

        builder.Options.PrintLogo = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.PrintResults = false;
        builder.Options.PrintDependencyChains = false;
        builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Silent;

        return builder;
    }

    [Test]
    public async Task ExecuteAsync_WhenDirectoryExists_DeletesDirectoryAndComputesBytesDeleted()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var artifactsPath = Path.Combine(tempDir, ".artifacts");
            Directory.CreateDirectory(artifactsPath);
            var dummyFile = Path.Combine(artifactsPath, "dummy.txt");
            await File.WriteAllTextAsync(dummyFile, "12345"); // 5 bytes

            var settings = CreateSettings();
            var mockDotNet = new Mock<IDotNet>();

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);

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
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_WhenDirectoryDoesNotExist_ReturnsSuccessAndExistedFalse()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var settings = CreateSettings();
            var mockDotNet = new Mock<IDotNet>();

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);

            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();
            var result = await summary.GetModule<CleanModule>();

            var cleanResult = result.ValueOrDefault;

            await Assert.That(cleanResult).IsNotNull();
            await Assert.That(cleanResult!.Success).IsTrue();
            await Assert.That(cleanResult.DirectoryExisted).IsFalse();
            await Assert.That(cleanResult.BytesDeleted).IsNull();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_ExecutesDotNetClean_AgainstWorkspaceSolution()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
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

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);

            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();
            var result = await summary.GetModule<CleanModule>();

            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(capturedOptions).IsNotNull();
            await Assert.That(capturedOptions!.ProjectSolution).IsEqualTo("test.sln");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_WithAdditionalCleanDirectories_DeletesAllConfiguredDirectories()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var artifactsPath = Path.Combine(tempDir, ".artifacts");
            Directory.CreateDirectory(artifactsPath);

            var nodeModulesPath = Path.Combine(tempDir, "node_modules");
            Directory.CreateDirectory(nodeModulesPath);

            var testResultsPath = Path.Combine(tempDir, "TestResults");
            Directory.CreateDirectory(testResultsPath);

            var settings = CreateSettings(["node_modules", "TestResults"]);
            var mockDotNet = new Mock<IDotNet>();

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);

            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();
            var result = await summary.GetModule<CleanModule>();

            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(Directory.Exists(artifactsPath)).IsFalse();
            await Assert.That(Directory.Exists(nodeModulesPath)).IsFalse();
            await Assert.That(Directory.Exists(testResultsPath)).IsFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_WithOverlappingCleanDirectories_HandlesGracefullyWithoutThrowing()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var artifactsPath = Path.Combine(tempDir, ".artifacts");
            Directory.CreateDirectory(artifactsPath);

            var innerPath = Path.Combine(artifactsPath, "nested");
            Directory.CreateDirectory(innerPath);

            var settings = CreateSettings([".artifacts/nested", ".artifacts"]); // Exact overlap and child path
            var mockDotNet = new Mock<IDotNet>();

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);

            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();
            var result = await summary.GetModule<CleanModule>();

            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(Directory.Exists(artifactsPath)).IsFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
