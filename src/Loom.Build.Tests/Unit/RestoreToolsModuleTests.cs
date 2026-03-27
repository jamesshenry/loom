using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.Models;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests.Unit;

public class RestoreToolsModuleTests
{
    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

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
            Run = new ExecutionOptions
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
        builder.Services.AddModule<RestoreToolsModule>();

        builder.Options.PrintLogo = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.PrintResults = false;
        builder.Options.PrintDependencyChains = false;
        builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Silent;

        return builder;
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

        var emptyCommandResult = new CommandResult(
            "",
            "",
            "",
            "",
            new Dictionary<string, string?>(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero,
            0
        );

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
            .ReturnsAsync(emptyCommandResult);

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
            .ReturnsAsync(emptyCommandResult);

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
            .ReturnsAsync(emptyCommandResult);

        mockDotNet.Setup(d => d.New).Returns(mockNew.Object);
        mockDotNet.Setup(d => d.Tool).Returns(mockTool.Object);

        newOptions = outNewOptions;
        restoreOptions = outRestoreOptions;
        toolOptions = outToolOptions;
    }

    [Test]
    public async Task Configure_SkipsExecution_WhenNoToolsAreRequired()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var settings = CreateSettings(requiresMinVer: false, requiresVelopack: false);
            var mockDotNet = new Mock<IDotNet>();

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();
            var result = await summary.GetModule<RestoreToolsModule>();

            await Assert.That(result.SkipDecisionOrDefault).IsNotNull();
            await Assert.That(result.SkipDecisionOrDefault!.ShouldSkip).IsTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_CreatesManifest_WhenManifestIsMissing()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            var settings = CreateSettings(requiresMinVer: true, requiresVelopack: false);
            var mockDotNet = new Mock<IDotNet>();

            SetupDotNetMocks(mockDotNet, out var newOptions, out _, out _);

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            await pipeline.RunAsync();

            await Assert.That(newOptions).Count().IsEqualTo(1);
            await Assert.That(newOptions[0].Arguments).IsNotNull();
            await Assert.That(newOptions[0].Arguments!).Contains("tool-manifest");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_DoesNotCreateManifest_WhenManifestExists()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            // Create a fake manifest
            System.IO.File.WriteAllText(Path.Combine(tempDir, "dotnet-tools.json"), "{}");

            var settings = CreateSettings(requiresMinVer: true, requiresVelopack: false);
            var mockDotNet = new Mock<IDotNet>();

            SetupDotNetMocks(mockDotNet, out var newOptions, out _, out _);

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            await pipeline.RunAsync();

            await Assert.That(newOptions).IsEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_InstallsRequiredTools_Always()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            System.IO.File.WriteAllText(Path.Combine(tempDir, "dotnet-tools.json"), "{}");

            // Context requires both minver-cli and vpk
            var settings = CreateSettings(requiresMinVer: true, requiresVelopack: true);
            var mockDotNet = new Mock<IDotNet>();

            SetupDotNetMocks(mockDotNet, out _, out _, out var toolOptions);

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            await pipeline.RunAsync();

            // Should have 2 install commands
            var installCommands = toolOptions.Where(o => o.Arguments!.Contains("install")).ToList();
            await Assert.That(installCommands).Count().IsEqualTo(2);
            await Assert
                .That(installCommands.Any(o => o.Arguments!.Contains("minver-cli")))
                .IsTrue();
            await Assert.That(installCommands.Any(o => o.Arguments!.Contains("vpk"))).IsTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_RestoresTools_AtEndOfExecution()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            System.IO.File.WriteAllText(Path.Combine(tempDir, "dotnet-tools.json"), "{}");

            var settings = CreateSettings(requiresMinVer: true, requiresVelopack: false);
            var mockDotNet = new Mock<IDotNet>();

            SetupDotNetMocks(mockDotNet, out _, out var restoreOptions, out _);

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            await pipeline.RunAsync();

            await Assert.That(restoreOptions).Count().IsEqualTo(1);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ExecuteAsync_ReturnsRestoreToolsResult_WrappingCommandResult()
    {
        var tempDir = CreateTemporaryDirectory();
        try
        {
            System.IO.File.WriteAllText(Path.Combine(tempDir, "dotnet-tools.json"), "{}");

            var settings = CreateSettings(requiresMinVer: true, requiresVelopack: false);
            var mockDotNet = new Mock<IDotNet>();

            SetupDotNetMocks(mockDotNet, out _, out _, out _);

            var builder = CreateSilentPipelineBuilder(settings, tempDir, mockDotNet);
            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();
            var result = await summary.GetModule<RestoreToolsModule>();
            var val = result.ValueOrDefault;

            await Assert.That(val).IsNotNull();
            await Assert.That(val!.CommandResult).IsNotNull(); // Checking it wrapped it in `RestoreToolsResult` properly
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
