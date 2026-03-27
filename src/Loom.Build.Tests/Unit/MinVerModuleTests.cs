using System.Collections.Concurrent;
using Loom.Config;
using Loom.MinVer;
using Loom.MinVer.Options;
using Loom.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.Context.Domains.Shell;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests.Unit;

public class MinVerModuleTests
{
    private static LoomSettings CreateSettings(Dictionary<string, string?>? artifactPrefixes = null)
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings
            {
                Solution = "test.sln",
                ArtifactsPath = ".artifacts",
            },
            Global = new GlobalSettings { Target = BuildTarget.Publish, Configuration = "Release" },
        };

        if (artifactPrefixes != null)
        {
            foreach (var kvp in artifactPrefixes)
            {
                settings.Artifacts.Add(
                    kvp.Key,
                    new ArtifactSettings
                    {
                        Type = ArtifactType.Nuget,
                        TagPrefix = kvp.Value,
                        Project = $"{kvp.Key}.csproj",
                    }
                );
            }
        }

        return settings;
    }

    [Test]
    public async Task ExecuteAsync_ResolvesMultipleArtifactPrefixes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var settings = CreateSettings(
                new Dictionary<string, string?> { ["App1"] = "v1-", ["App2"] = "v2-" }
            );
            var loomContext = new LoomContext(settings, tempDir);

            var mockMinVer = new Mock<IMinVer>();
            mockMinVer
                .Setup(x =>
                    x.Run(
                        It.IsAny<MinVerBaseOptions?>(),
                        It.IsAny<CommandExecutionOptions?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    (MinVerBaseOptions? opts, CommandExecutionOptions? _, CancellationToken _) =>
                    {
                        var prefix = (opts as DotNetMinVerOptions)?.TagPrefix;
                        var versionStr = prefix switch
                        {
                            "v1-" => "1.0.1",
                            "v2-" => "2.0.2",
                            _ => "0.0.0",
                        };

                        return new MinVerVersion(versionStr);
                    }
                );

            var builder = Pipeline.CreateBuilder();
            builder.Services.AddSingleton(loomContext);

            builder.Services.AddSingleton(mockMinVer.Object);
            builder.Services.AddModule<MinVerModule>();

            builder.Options.PrintLogo = false;
            builder.Options.ShowProgressInConsole = false;

            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();

            var result = await summary.GetModule<MinVerModule>();
            var versions = result.ValueOrDefault!;

            await Assert.That(versions.GetVersion("v1-").ToString()).IsEqualTo("1.0.1");
            await Assert.That(versions.GetVersion("v2-").ToString()).IsEqualTo("2.0.2");
            await Assert.That(versions.GetVersion(null).ToString()).IsEqualTo("0.0.0");
            await Assert.That(versions.GetVersion(string.Empty).ToString()).IsEqualTo("0.0.0");

            mockMinVer.Verify(
                x =>
                    x.Run(
                        It.Is<DotNetMinVerOptions>(o => o.TagPrefix == "v1-"),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
            mockMinVer.Verify(
                x =>
                    x.Run(
                        It.Is<DotNetMinVerOptions>(o => o.TagPrefix == "v2-"),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
            mockMinVer.Verify(
                x =>
                    x.Run(
                        It.Is<DotNetMinVerOptions>(o => o.TagPrefix == null),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
