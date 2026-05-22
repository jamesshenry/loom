using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.Models;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests.Unit;

public class RestoreToolsModuleTests
{
    private static LoomSettings CreateSettings(
        bool requiresMinVer = true,
        bool requiresVelopack = false
    )
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                ArtifactsPath = ".artifacts",
            },
            Global = new GlobalSettings
            {
                Target = requiresMinVer ? BuildTarget.Publish : BuildTarget.Build,
                Configuration = "Release",
            },
        };

        if (requiresMinVer)
        {
            settings.Artifacts.Add(
                "MyNuget",
                new ArtifactSettings { Type = ArtifactType.Nuget, Project = "MyNuget.csproj" }
            );
        }

        if (requiresVelopack)
        {
            settings.Artifacts.Add(
                "MyDesktopApp",
                new ArtifactSettings { Type = ArtifactType.Velopack, Project = "MyApp.csproj" }
            );
        }

        return settings;
    }

    private static void SetupDotNetMocks(
        Mock<IDotNet> mockDotNet,
        out List<DotNetNewOptions> newOptions,
        out List<DotNetToolRestoreOptions> restoreOptions,
        out List<DotNetToolOptions> toolOptions
    )
    {
        var outNewOptions = new List<DotNetNewOptions>();
        var outRestoreOptions = new List<DotNetToolRestoreOptions>();
        var outToolOptions = new List<DotNetToolOptions>();

        var mockCommand = new Mock<ICommand>();

        var mockNew = new Mock<DotNetNew>(mockCommand.Object);
        mockNew
            .Setup(n =>
                n.Execute(
                    It.IsAny<DotNetNewOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DotNetNewOptions, CommandExecutionOptions, CancellationToken>(
                (opts, _, _) => outNewOptions.Add(opts)
            )
            .ReturnsAsync(TestHelpers.EmptyCommandResult());

        var mockTool = new Mock<DotNetTool>(mockCommand.Object);
        mockTool
            .Setup(t =>
                t.Restore(
                    It.IsAny<DotNetToolRestoreOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DotNetToolRestoreOptions, CommandExecutionOptions, CancellationToken>(
                (opts, _, _) => outRestoreOptions.Add(opts)
            )
            .ReturnsAsync(TestHelpers.EmptyCommandResult());

        mockTool
            .Setup(t =>
                t.Execute(
                    It.IsAny<DotNetToolOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DotNetToolOptions, CommandExecutionOptions, CancellationToken>(
                (opts, _, _) => outToolOptions.Add(opts)
            )
            .ReturnsAsync(TestHelpers.EmptyCommandResult());

        mockDotNet.Setup(d => d.New).Returns(mockNew.Object);
        mockDotNet.Setup(d => d.Tool).Returns(mockTool.Object);

        newOptions = outNewOptions;
        restoreOptions = outRestoreOptions;
        toolOptions = outToolOptions;
    }

    [Test]
    public async Task Configure_SkipsExecution_WhenNoToolsAreRequired()
    {
        const string tempDir = "/fake/workspace";
        var settings = CreateSettings(requiresMinVer: false, requiresVelopack: false);
        var mockDotNet = new Mock<IDotNet>();

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<RestoreToolsModule>();
            }
        );
        builder.AddMockFileSystem();
        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var result = await summary.GetModule<RestoreToolsModule>();

        await Assert.That(result.SkipDecisionOrDefault).IsNotNull();
        await Assert.That(result.SkipDecisionOrDefault!.ShouldSkip).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_CreatesManifest_WhenManifestIsMissing()
    {
        const string tempDir = "/fake/workspace";
        var settings = CreateSettings(requiresMinVer: true, requiresVelopack: false);
        var mockDotNet = new Mock<IDotNet>();

        SetupDotNetMocks(mockDotNet, out var newOptions, out _, out _);

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<RestoreToolsModule>();
            }
        );
        builder.AddMockFileSystem();
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        await Assert.That(newOptions).Count().IsEqualTo(1);
        await Assert.That(newOptions[0].Arguments).IsNotNull();
        await Assert.That(newOptions[0].Arguments!).Contains("tool-manifest");
    }

    [Test]
    public async Task ExecuteAsync_DoesNotCreateManifest_WhenManifestExists()
    {
        const string tempDir = "/fake/workspace";

        var settings = CreateSettings(requiresMinVer: true, requiresVelopack: false);
        var mockDotNet = new Mock<IDotNet>();

        SetupDotNetMocks(mockDotNet, out var newOptions, out _, out _);

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<RestoreToolsModule>();
            }
        );
        var mockFs = builder.AddMockFileSystem();
        mockFs
            .Setup(x => x.FileExists(It.Is<string>(s => s.Contains("dotnet-tools.json"))))
            .Returns(true);
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        await Assert.That(newOptions).IsEmpty();
    }

    // [Test]
    // public async Task ExecuteAsync_InstallsRequiredTools_Always()
    // {
    //     var tempDir = CreateTemporaryDirectory();
    //     try
    //     {
    //         System.IO.File.WriteAllText(Path.Combine(tempDir, "dotnet-tools.json"), "{}");

    //         // Context requires both minver-cli and vpk
    //         var settings = CreateSettings(requiresMinVer: true, requiresVelopack: true);
    //         var mockDotNet = new Mock<IDotNet>();

    //         SetupDotNetMocks(mockDotNet, out _, out _, out var toolOptions);

    //         var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
    //         var pipeline = await builder.BuildAsync();
    //         await pipeline.RunAsync();

    //         // Should have 2 install commands
    //         var installCommands = toolOptions.Where(o => o.Arguments!.Contains("install")).ToList();
    //         await Assert.That(installCommands).Count().IsEqualTo(2);
    //         await Assert
    //             .That(installCommands.Any(o => o.Arguments!.Contains("minver-cli")))
    //             .IsTrue();
    //         await Assert.That(installCommands.Any(o => o.Arguments!.Contains("vpk"))).IsTrue();
    //     }
    //     finally
    //     {
    //         if (Directory.Exists(tempDir))
    //             Directory.Delete(tempDir, true);
    //     }
    // }

    [Test]
    public async Task ExecuteAsync_RestoresTools_AtEndOfExecution()
    {
        const string tempDir = "/fake/workspace";

        var settings = CreateSettings(requiresMinVer: true, requiresVelopack: false);
        var mockDotNet = new Mock<IDotNet>();

        SetupDotNetMocks(mockDotNet, out _, out var restoreOptions, out _);

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<RestoreToolsModule>();
            }
        );
        var mockFs = builder.AddMockFileSystem();
        mockFs
            .Setup(x => x.FileExists(It.Is<string>(s => s.Contains("dotnet-tools.json"))))
            .Returns(true);
        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();

        await Assert.That(restoreOptions).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ExecuteAsync_ReturnsRestoreToolsResult_WrappingCommandResult()
    {
        const string tempDir = "/fake/workspace";

        var settings = CreateSettings(requiresMinVer: true, requiresVelopack: false);
        var mockDotNet = new Mock<IDotNet>();

        SetupDotNetMocks(mockDotNet, out _, out _, out _);

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, tempDir),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<RestoreToolsModule>();
            }
        );
        var mockFs = builder.AddMockFileSystem();
        mockFs
            .Setup(x => x.FileExists(It.Is<string>(s => s.Contains("dotnet-tools.json"))))
            .Returns(true);
        var pipeline = await builder.BuildAsync();
        var summary = await pipeline.RunAsync();
        var result = await summary.GetModule<RestoreToolsModule>();
        var val = result.ValueOrDefault;

        await Assert.That(val).IsNotNull();
        await Assert.That(val!.CommandResult).IsNotNull(); // Checking it wrapped it in `RestoreToolsResult` properly
    }
}
