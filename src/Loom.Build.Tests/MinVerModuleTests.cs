using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Enums;
using ModularPipelines.Extensions;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using Moq;

namespace Loom.Build.Tests;

public class MinVerModuleTests
{
    private LoomContext _loomContext = null!;

    [Before(Test)]
    public void Setup()
    {
        var settings = new LoomSettings
        {
            Project = new ProjectConfig { Solution = "test.sln", EntryProject = "test.csproj" },
            Build = new BuildConfig { Target = BuildTarget.Build },
        };
        _loomContext = new LoomContext(settings);
    }

    private PipelineBuilder BuildPipeline(string? shellOutput)
    {
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(_loomContext);
        builder.Services.AddSingleton(new Mock<IFileSystemProvider>().Object);
        builder.Services.AddSingleton(shellOutput ?? "");
        builder.Services.AddModule<MinVerModuleWrapper>();
        return builder;
    }

    [Test]
    public async Task ExecuteAsync_ReturnsVersionFromShell()
    {
        var builder = BuildPipeline("2.5.0");
        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();

        var module = summary.GetModule<MinVerModuleWrapper>();
        var result = await module;
        await Assert.That(result.ValueOrDefault).IsEqualTo("2.5.0");
    }

    [Test]
    public async Task ExecuteAsync_TrimsWhitespaceFromVersion()
    {
        var builder = BuildPipeline("  1.0.0-preview.1  \n");
        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();

        var module = summary.GetModule<MinVerModuleWrapper>();
        var result = await module;
        await Assert.That(result.ValueOrDefault).IsEqualTo("1.0.0-preview.1");
    }

    [Test]
    public async Task ExecuteAsync_Throws_WhenVersionIsEmpty()
    {
        var builder = BuildPipeline("");
        var pipeline = await builder.BuildAsync();

        await Assert.ThrowsAsync<Exception>(() => pipeline.RunAsync());
    }

    [Test]
    public async Task ExecuteAsync_Throws_WhenVersionIsWhitespace()
    {
        var builder = BuildPipeline("   ");
        var pipeline = await builder.BuildAsync();

        await Assert.ThrowsAsync<Exception>(() => pipeline.RunAsync());
    }

    [Test]
    public async Task MinverOptions_HasCorrectDefaultPreReleaseIdentifiers()
    {
        var options = new MinverOptions();
        await Assert.That(options.Tool).IsEqualTo("dotnet");
        await Assert.That(options.Arguments).Contains("minver");
        await Assert.That(options.Arguments).Contains("--default-pre-release-identifiers");
        await Assert.That(options.Arguments).Contains("preview.0");
    }
}

[ModuleCategory("Test")]
public class MinVerModuleWrapper(string shellOutput) : Module<string>
{
    protected override Task<string?> ExecuteAsync(IModuleContext context, CancellationToken ct)
    {
        string version = shellOutput.Trim();

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new Exception(
                "MinVer output was empty. Check if Git is initialized and tags exist."
            );
        }

        return Task.FromResult<string?>(version);
    }
}
