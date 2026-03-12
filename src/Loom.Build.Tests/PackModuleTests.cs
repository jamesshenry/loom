using System.Reflection;
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
using File = ModularPipelines.FileSystem.File;

namespace Loom.Build.Tests;

public class PackModuleTests
{
    private LoomContext _loomContext = null!;

    [Before(Test)]
    public void Setup()
    {
        var settings = new LoomSettings
        {
            Project = new ProjectConfig { Solution = "test.sln", EntryProject = "test.csproj" },
            Build = new BuildConfig { Target = BuildTarget.NugetUpload },
        };
        _loomContext = new LoomContext(settings);
    }

    private PipelineBuilder BuildPipeline(LoomContext? ctx = null)
    {
        var context = ctx ?? _loomContext;
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(context);
        builder.Services.AddSingleton(new Mock<IFileSystemProvider>().Object);
        builder.Services.AddModule<PackModuleWrapper>();
        return builder;
    }

    [Test]
    public async Task ExecuteAsync_ReturnsEmptyList()
    {
        // PackModule currently returns an empty list (pack call commented out)
        var builder = BuildPipeline();
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        var module = pipeline.Services.GetRequiredService<PackModuleWrapper>();
        var result = await module;
        await Assert.That(result.ValueOrDefault).IsNotNull();
        await Assert.That(result.ValueOrDefault).IsEmpty();
    }

    [Test]
    public async Task ExecuteAsync_DoesNotThrow()
    {
        var builder = BuildPipeline();
        var pipeline = await builder.BuildAsync();

        // Pipeline should complete successfully
        var summary = await pipeline.RunAsync();
        await Assert.That(summary.Status).IsEqualTo(Status.Successful);
    }
}

[ModuleCategory("Test")]
public class PackModuleWrapper(LoomContext ctx) : Module<List<File>>
{
    protected override async Task<List<File>?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var realModule = new PackModule(ctx);
        var method = typeof(PackModule).GetMethod(
            "ExecuteAsync",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        return await (Task<List<File>?>)method!.Invoke(realModule, [context, ct])!;
    }
}
