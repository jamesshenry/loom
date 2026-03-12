using System.Reflection;
using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularPipelines;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.Extensions;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;
using Moq;
using File = ModularPipelines.FileSystem.File;

namespace Loom.Build.Tests;

public class NugetUploadModuleTests
{
    private Mock<IDotNet> _mockDotNet = null!;
    private Mock<DotNetNuget> _mockNuget = null!;
    private LoomContext _loomContext = null!;
    private File _package = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockDotNet = new Mock<IDotNet>();

        // DotNetNuget has a single-parameter constructor from its source generator.
        // We provide null to allow Moq to create the proxy.
        _mockNuget = new Mock<DotNetNuget>(null!);
        _mockDotNet.Setup(x => x.Nuget).Returns(_mockNuget.Object);

        _mockNuget
            .Setup(x =>
                x.Push(It.IsAny<DotNetNugetPushOptions>(), null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CommandResult)null!);

        var settings = new LoomSettings
        {
            Project = new ProjectConfig { Solution = "test.sln", EntryProject = "test.csproj" },
            Build = new BuildConfig { Target = BuildTarget.Release },
            Nuget = new NugetConfig { ApiKey = "test-api-key" },
        };
        _loomContext = new LoomContext(settings);
        _package = (File)"test.1.0.0.nupkg"!;
    }

    private PipelineBuilder BuildPipeline(
        LoomContext? loomContext = null,
        IEnumerable<File>? packages = null,
        bool isDryRun = false
    )
    {
        var ctx = loomContext ?? _loomContext;
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(_mockDotNet.Object);
        builder.Services.AddSingleton(ctx);
        builder.Services.AddSingleton(new Mock<IFileSystemProvider>().Object);
        builder.Services.AddModule(_ => new NugetUploadWrapperModule(ctx, packages, isDryRun));
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
    public async Task ExecuteAsync_ValidPackages_CallsDotNetPushWithCorrectOptions()
    {
        var builder = BuildPipeline(packages: new[] { _package });
        await (await builder.BuildAsync()).RunAsync();

        _mockNuget.Verify(
            x =>
                x.Push(
                    It.Is<DotNetNugetPushOptions>(o =>
                        o.Path == _package.Path
                        && o.ApiKey == "test-api-key"
                        && o.Source == "https://api.nuget.org/v3/index.json"
                    ),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ExecuteAsync_MissingApiKey_SkipsPush()
    {
        var settings = new LoomSettings
        {
            Project = new ProjectConfig { Solution = "test.sln", EntryProject = "test.csproj" },
            Build = new BuildConfig { Target = BuildTarget.Release },
            Nuget = new NugetConfig { ApiKey = null },
        };
        var loomContextWithoutKey = new LoomContext(settings);

        var builder = BuildPipeline(
            loomContext: loomContextWithoutKey,
            packages: new[] { _package }
        );
        await (await builder.BuildAsync()).RunAsync();

        _mockNuget.Verify(
            x =>
                x.Push(
                    It.IsAny<DotNetNugetPushOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ExecuteAsync_DryRun_SkipsPush()
    {
        var builder = BuildPipeline(packages: new[] { _package }, isDryRun: true);
        await (await builder.BuildAsync()).RunAsync();

        _mockNuget.Verify(
            x =>
                x.Push(
                    It.IsAny<DotNetNugetPushOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ExecuteAsync_EmptyPackages_NeverCallsPush()
    {
        var builder = BuildPipeline(packages: Array.Empty<File>());
        await (await builder.BuildAsync()).RunAsync();

        _mockNuget.Verify(
            x =>
                x.Push(
                    It.IsAny<DotNetNugetPushOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ExecuteAsync_MultiplePackages_CallsPushForEach()
    {
        var packages = new[] { (File)"test1.nupkg"!, (File)"test2.nupkg"! };

        var builder = BuildPipeline(packages: packages);
        await (await builder.BuildAsync()).RunAsync();

        _mockNuget.Verify(
            x => x.Push(It.IsAny<DotNetNugetPushOptions>(), null, It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );

        foreach (var pkg in packages)
        {
            _mockNuget.Verify(
                x =>
                    x.Push(
                        It.Is<DotNetNugetPushOptions>(o =>
                            o.Path == pkg.Path && o.ApiKey == "test-api-key"
                        ),
                        null,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        }
    }
}

/// <summary>
/// Dependency-free wrapper that delegates execution to NugetUploadModule.
/// Necessary because NugetUploadModule has [DependsOn&lt;PackModule&gt;], which would require
/// registering the full module chain in every test.
/// </summary>
[ModuleCategory("Test")]
public class NugetUploadWrapperModule(
    LoomContext loomContext,
    IEnumerable<File>? packages = null,
    bool isDryRun = false
) : Module<CommandResult[]>
{
    protected override async Task<CommandResult[]?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var realModule = new NugetUploadModule(loomContext, packages) { IsDryRun = isDryRun };

        var method = typeof(NugetUploadModule).GetMethod(
            "ExecuteAsync",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        return await (Task<CommandResult[]?>)method!.Invoke(realModule, [context, ct])!;
    }
}
