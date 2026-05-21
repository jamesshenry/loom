using Loom.Config;
using Loom.Modules;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Options;

using Moq;

namespace Loom.Build.Tests.Unit;

public class NugetUploadModuleTests
{
    private static LoomSettings CreateSettings(
        bool withNugetArtifact = true,
        bool enableNugetUpload = true
    )
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                ArtifactsPath = ".artifacts",
                EnableNugetUpload = enableNugetUpload,
            },
            Global = new GlobalSettings { Target = BuildTarget.Publish, Configuration = "Release" },
            Nuget = new NugetSettings { ApiKey = "test-api-key" },
        };

        if (withNugetArtifact)
        {
            settings.Artifacts.Add(
                "MyPackage",
                new ArtifactSettings { Type = ArtifactType.Nuget, Project = "MyPackage.csproj" }
            );
        }

        return settings;
    }

    [Test]
    public async Task Configure_SkipsExecution_WhenNoNugetArtifactsDefined()
    {
        using var tempDir = new TempDirectory();
        var settings = CreateSettings(withNugetArtifact: false);
        var mockDotNet = new Mock<IDotNet>();
        var builder = TestHelpers.CreateSilentPipelineBuilder(new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule(x => new FakePackModule(x.GetRequiredService<LoomContext>()) as PackModule);
                services.AddModule<NugetUploadModule>();
            });

        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var result = await summary.GetModule<NugetUploadModule>();

        await Assert.That(result.SkipDecisionOrDefault).IsNotNull();
        await Assert.That(result.SkipDecisionOrDefault!.ShouldSkip).IsTrue();
        await Assert.That(result.SkipDecisionOrDefault.Reason).Contains("No nuget artifacts");
    }

    [Test]
    public async Task Configure_SkipsExecution_WhenNugetUploadDisabled()
    {
        using var tempDir = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(tempDir, ".artifacts", "nuget"));
        var settings = CreateSettings(withNugetArtifact: true, enableNugetUpload: false);
        var mockDotNet = new Mock<IDotNet>();
        var builder = TestHelpers.CreateSilentPipelineBuilder(new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule(x => new FakePackModule(x.GetRequiredService<LoomContext>()) as PackModule);
                services.AddModule<NugetUploadModule>();
            });

        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var result = await summary.GetModule<NugetUploadModule>();

        await Assert.That(result.SkipDecisionOrDefault).IsNotNull();
        await Assert.That(result.SkipDecisionOrDefault!.ShouldSkip).IsTrue();
        await Assert
            .That(result.SkipDecisionOrDefault.Reason)
            .Contains("disabled in workspace settings");
    }

    [Test]
    public async Task ExecuteAsync_PushesPackages_WhenConditionsAreMet()
    {
        using var tempDir = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(tempDir, ".artifacts", "nuget"));
        var settings = CreateSettings(withNugetArtifact: true, enableNugetUpload: true);

        var capturedOptions = new List<DotNetNugetPushOptions>();

        var mockDotNet = new Mock<IDotNet>();



        var mockCommand = new Mock<ICommand>();
        var mockNuget = new Mock<DotNetNuget>(mockCommand.Object);

        mockNuget
            .Setup(n =>
                n.Push(
                    It.IsAny<DotNetNugetPushOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DotNetNugetPushOptions, CommandExecutionOptions, CancellationToken>(
                (opts, _, _) => capturedOptions.Add(opts)
            )
                .ReturnsAsync(TestHelpers.EmptyCommandResult());

        mockDotNet.Setup(d => d.Nuget).Returns(mockNuget.Object);

        var loomContext = new LoomContext(settings, tempDir);

        // Simulate CI mode to avoid ctx.IsRunningLocally() skipping it
        Environment.SetEnvironmentVariable("LOOM_IGNORE_LOCAL_CHECK", "true");
        try
        {
            var builder = TestHelpers.CreateSilentPipelineBuilder(loomContext,
                services =>
                {
                    services.AddSingleton(mockDotNet.Object);
                    services.AddModule(x => new FakePackModule(x.GetRequiredService<LoomContext>()) as PackModule);
                    services.AddModule<NugetUploadModule>();
                });

            var pipeline = await builder.BuildAsync();
            await pipeline.RunAsync();

            await Assert.That(capturedOptions).Count().IsEqualTo(2);
            await Assert.That(capturedOptions[0].Path).EndsWith("package1.nupkg");
            await Assert.That(capturedOptions[1].Path).EndsWith("package2.nupkg");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOOM_IGNORE_LOCAL_CHECK", null);
        }
    }
}
