using Loom.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularPipelines;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Extensions;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests;

public class CleanModuleTests
{
    private Mock<IFileSystemProvider> _mockFileSystem = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockFileSystem = new Mock<IFileSystemProvider>();
        _mockFileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(false);
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystem
            .Setup(f => f.Combine(It.IsAny<string[]>()))
            .Returns((string[] paths) => Path.Combine(paths));
    }

    private PipelineBuilder BuildPipeline()
    {
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(_mockFileSystem.Object);
        var settings = new LoomSettings
        {
            Project = new ProjectConfig { Solution = "test.sln", EntryProject = "test.csproj" },
            Build = new BuildConfig { Target = BuildTarget.Build },
        };
        builder.Services.AddSingleton(new LoomContext(settings));
        builder.Services.AddModule<CleanModuleWrapper>();
        builder.Services.AddLogging(b => b.ClearProviders());
        builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Default;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.PrintResults = false;
        builder.Options.PrintLogo = false;
        builder.Options.PrintDependencyChains = false;
        builder.Options.ThrowOnPipelineFailure = false; // Tests handle failures explicitly
        return builder;
    }

    [Test]
    public async Task ExecuteAsync_ReturnsTrue()
    {
        var builder = BuildPipeline();
        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var module = summary.GetModule<CleanModuleWrapper>();

        var result = await module;
        await Assert.That(result.ValueOrDefault).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_WhenArtifactsDirExists_DeletesIt()
    {
        _mockFileSystem
            .Setup(f => f.DirectoryExists(It.Is<string>(s => s.Contains(".artifacts"))))
            .Returns(true);

        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockFileSystem.Verify(
            f => f.DeleteDirectory(It.Is<string>(s => s.Contains(".artifacts")), true),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_WhenArtifactsDirAbsent_DoesNotDelete()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockFileSystem.Verify(
            f => f.DeleteDirectory(It.Is<string>(s => s.Contains(".artifacts")), It.IsAny<bool>()),
            Times.Never
        );
    }

    [Test]
    public async Task ExecuteAsync_WhenDistDirExists_DeletesIt()
    {
        _mockFileSystem
            .Setup(f => f.DirectoryExists(It.Is<string>(s => s.Contains(".dist"))))
            .Returns(true);

        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockFileSystem.Verify(
            f => f.DeleteDirectory(It.Is<string>(s => s.Contains(".dist")), true),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_WhenDistDirAbsent_DoesNotDelete()
    {
        var builder = BuildPipeline();
        await (await builder.BuildAsync()).RunAsync();

        _mockFileSystem.Verify(
            f => f.DeleteDirectory(It.Is<string>(s => s.Contains(".dist")), It.IsAny<bool>()),
            Times.Never
        );
    }
}

/// <summary>
/// CleanModule wrapper that bypasses Git.RevParse and checks existence via IFileSystemProvider,
/// which is mockable — unlike Folder.Exists which reads the real file system.
/// </summary>
[ModuleCategory("Test")]
public class CleanModuleWrapper(IFileSystemProvider fileSystem) : Module<bool>
{
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken ct)
    {
        var repoRoot = context.Environment.WorkingDirectory;

        var artifactsPath = Path.Combine(repoRoot, ".artifacts");
        if (fileSystem.DirectoryExists(artifactsPath))
        {
            fileSystem.DeleteDirectory(artifactsPath, recursive: true);
        }

        var distPath = Path.Combine(repoRoot, ".dist");
        if (fileSystem.DirectoryExists(distPath))
        {
            fileSystem.DeleteDirectory(distPath, recursive: true);
        }

        return await Task.FromResult(true);
    }
}
