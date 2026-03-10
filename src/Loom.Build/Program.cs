using ConsoleAppFramework;
using Loom;
using Loom.Config;
using ModularPipelines;
#pragma warning disable ConsoleUse // Use of Console detected

string repoRoot = Directory.GetRepoRoot(Directory.GetCurrentDirectory());

Directory.SetCurrentDirectory(repoRoot);

var app = ConsoleApp.Create();
app.Add(
    "",
    async (
        string? rid = null,
        string? version = null,
        BuildTarget? target = null,
        bool? quick = null,
        bool? skipPreparation = null,
        bool? skipPackaging = null,
        bool? skipDelivery = null
    ) =>
    {
        var cliOptions = new BuildConfig
        {
            Rid = rid,
            Version = version,
            Target = target,
            Quick = quick,
            SkipPreparation = skipPreparation,
            SkipPackaging = skipPackaging,
            SkipDelivery = skipDelivery,
        };
        var config = new ConfigurationBuilder()
            .SetBasePath(repoRoot)
            .AddJsonFile("my-build.json", optional: true)
            .AddInMemoryCollection(cliOptions.ToInMemoryCollection())
            .AddEnvironmentVariables()
            .Build();
        var settings = new BuildSettings();
        config.Bind(settings);
        var context = new BuildContext(settings);

        var builder = Pipeline.CreateBuilder(args);
        builder.Configuration.AddConfiguration(config);
        builder.Services.AddServices(context);
        builder.Options.PrintLogo = false;
        builder.Options.ShowProgressInConsole = true;
        builder.Options.IgnoreCategories = [.. context.GetIgnoredCategories()];
        var pipeline = await builder.BuildAsync();

        await pipeline.RunAsync();
    }
);
await app.RunAsync(args);

#if DEBUG
Console.ReadLine();
#pragma warning restore ConsoleUse // Use of Console detected
#endif
