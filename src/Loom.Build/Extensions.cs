using System.Runtime.InteropServices;

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
            while (dir is not null)
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

            var resolved = new List<ResolvedArtifact>();
            foreach (var (name, art) in settings.Artifacts)
            {
                var rid = art.Rid ?? settings.Global.Rid ?? GetDefaultRid();

                var projectPath = Path.Combine(workingDirectory, art.Project);
                bool isAot = File.Exists(projectPath) &&
                             File.ReadAllText(projectPath).Contains("<PublishAot>true", StringComparison.OrdinalIgnoreCase);

                bool canBuild = !isAot || IsNativeHostCompatible(rid);

                resolved.Add(new ResolvedArtifact(name, art, rid, isAot, canBuild));
            }

            var context = new LoomContext(settings, workingDirectory)
            {
                ResolvedArtifacts = resolved.AsReadOnly()
            };
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
        public static bool IsNativeHostCompatible(string targetRid)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return targetRid.StartsWith("win", StringComparison.OrdinalIgnoreCase);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return targetRid.StartsWith("linux", StringComparison.OrdinalIgnoreCase);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return targetRid.StartsWith("osx", StringComparison.OrdinalIgnoreCase);

            return false;
        }
        public static string GetDefaultRid()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "win-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? "osx-arm64"
                    : "osx-x64";
            return "linux-x64";
        }

    }

    extension(IConfiguration configuration) { }

    extension(ArtifactSettings artifact)
    {
        public bool IsPublishable()
        {
            return artifact.Type == ArtifactType.Executable || artifact.Type == ArtifactType.Velopack;
        }
    }
}

public record ResolvedArtifact(
    string Name,
    ArtifactSettings Settings,
    string Rid,
    bool IsAot,
    bool CanBuildOnHost
);
