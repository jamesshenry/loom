using System.Diagnostics;
using System.Text.Json;
using Loom.Config;
using ModularPipelines;
using ModularPipelines.Models;
using ModularPipelines.Options;
using TUnit.Core.Interfaces;

namespace Loom.Build.Tests;

public class FixtureHarness : IAsyncInitializer, IAsyncDisposable
{
    public string WorkingDir { get; private set; } = "";
    public PipelineSummary? InitialSummary { get; protected set; }

    protected virtual void ConfigureSettings(LoomSettings settings) =>
        settings.Workspace = new WorkspaceSettings { Solution = "Fixtures.slnx" };

    protected virtual Task RunPipelineAsync() => Task.CompletedTask;

    // IAsyncInitializer — called once by TUnit before any test in the class runs
    public async Task InitializeAsync()
    {
        var settings = new LoomSettings();
        ConfigureSettings(settings);

        // Write the embedded bundle to a temp file so git can clone from it
        var bundlePath = Path.Combine(
            Path.GetTempPath(),
            $"loom-fixture-{Guid.NewGuid():N}.bundle"
        );
        using (
            var bundleStream =
                typeof(FixtureHarness).Assembly.GetManifestResourceStream("fixtures.bundle")
                ?? throw new InvalidOperationException(
                    "fixtures.bundle not found as embedded resource."
                )
        )
        await using (var fileStream = File.Create(bundlePath))
            await bundleStream.CopyToAsync(fileStream);

        var tempDir = Path.Combine(Path.GetTempPath(), "loom-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await RunGitAsync($"clone \"{bundlePath}\" \"{tempDir}\"");
        }
        finally
        {
            File.Delete(bundlePath);
        }

        var buildDir = Path.Combine(tempDir, ".build");
        Directory.CreateDirectory(buildDir);

        var json = JsonSerializer.Serialize(settings, LoomSettingsContext.Default.LoomSettings);
        await File.WriteAllTextAsync(Path.Combine(buildDir, "loom.json"), json);

        WorkingDir = tempDir;
        await RunPipelineAsync();
    }

    public async Task<PipelineSummary> RunAsync(BuildTarget target)
    {
        var loomJsonPath = Path.Combine(WorkingDir, ".build", "loom.json");
        var runSettings = new GlobalSettings { Target = target };

        var builder = Pipeline.CreateBuilder();
        builder.Services.AddLoomContext(loomJsonPath, runSettings, WorkingDir);
        builder.Services.AddModules();
        builder.Options.PrintLogo = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.PrintResults = false;
        builder.Options.PrintDependencyChains = false;
        builder.Options.ThrowOnPipelineFailure = false;
        builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Silent;
        builder.Options.RunOnlyCategories = LoomConfig.GetPipelineCategories(target);

        var pipeline = await builder.BuildAsync();

        return await pipeline.RunAsync();
    }

    private static async Task RunGitAsync(string args)
    {
        using var proc = Process.Start(
            new ProcessStartInfo("git", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            }
        )!;
        await proc.WaitForExitAsync();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Directory.Exists(WorkingDir))
                Directory.Delete(WorkingDir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
        await ValueTask.CompletedTask;
    }
}
