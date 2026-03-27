using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Loom;

public static class Extensions
{
    extension(Directory)
    {
        public static string GetRepoRoot(string? startPath = default)
        {
            var dir = new DirectoryInfo(startPath ?? Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (dir.GetDirectories(".git").Length != 0 || dir.GetFiles("*.slnx").Length != 0)
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            return startPath ?? Directory.GetCurrentDirectory();
        }
    }
    extension(IServiceCollection services)
    {
        internal LoomContext AddLoomContext(
            string loomJsonPath,
            GlobalSettings runSettings,
            string? workingDirectory = null
        )
        {
            var configBuilder = new ConfigurationBuilder().SetBasePath(
                Path.GetDirectoryName(loomJsonPath)!
            );
            configBuilder.AddJsonFile(loomJsonPath, optional: false);

            var config = configBuilder
                .AddEnvironmentVariables()
                .AddUserSecrets<Program>()
                .AddInMemoryCollection(runSettings.ToInMemoryCollection())
                .Build();

            var settings = new LoomSettings()
            {
                Nuget = new() { ApiKey = config.GetSection("Nuget:ApiKey").Value ?? string.Empty },
                GithubAccessToken = config.GetSection("GITHUB_TOKEN").Value ?? string.Empty,
            };

            config.Bind(settings);
            workingDirectory ??= Environment.CurrentDirectory;
            var context = new LoomContext(settings, workingDirectory);
            services.AddSingleton(settings);
            services.AddSingleton(context);

            return context;
        }

        internal IServiceCollection AddModules()
        {
            services.AddModule<RestoreModule>();
            services.AddModule<RestoreToolsModule>();
            services.AddModule<MinVerModule>();
            services.AddModule<PackModule>();
            services.AddModule<VelopackReleaseModule>();
            services.AddModule<PublishModule>();
            services.AddModule<TestModule>();
            services.AddModule<NugetUploadModule>();
            services.AddModule<CleanModule>();
            services.AddModule<BuildModule>();
            services.AddModule<GitHubReleaseModule>();

            return services;
        }
    }

    extension(IConfiguration configuration) { }
}
