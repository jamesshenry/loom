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
        internal IServiceCollection AddServices(LoomContext context)
        {
            services.AddSingleton(context);

            switch (context.Target)
            {
                case BuildTarget.Release:
                    services.AddModule<VelopackReleaseModule>();
                    break;
                case BuildTarget.Publish:
                    services.AddModule<PublishModule>();
                    break;
                case BuildTarget.Test:
                    services.AddModule<TestModule>();
                    break;
                case BuildTarget.NugetUpload:
                    services.AddModule<NugetUploadModule>();
                    break;
                case BuildTarget.Clean:
                    services.AddModule<CleanModule>();
                    break;
                case BuildTarget.Build:
                    services.AddModule<BuildModule>();
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled enum value: {context.Target}");
            }
            return services;
        }
    }

    extension(IConfiguration config)
    {
        public ProjectConfig BindProjectMetadata()
        {
            var section = config.GetSection(nameof(ProjectConfig));
            return new ProjectConfig
            {
                Solution =
                    section[nameof(ProjectConfig.Solution)]
                    ?? throw new Exception(
                        $"{nameof(ProjectConfig)}:{nameof(ProjectConfig.Solution)} is missing"
                    ),
                EntryProject =
                    section[nameof(ProjectConfig.EntryProject)]
                    ?? throw new Exception(
                        $"{nameof(ProjectConfig)}:{nameof(ProjectConfig.EntryProject)} is missing"
                    ),
                VelopackId =
                    section[nameof(ProjectConfig.VelopackId)]
                    ?? throw new Exception(
                        $"{nameof(ProjectConfig)}:{nameof(ProjectConfig.VelopackId)} is missing"
                    ),
            };
        }

        public BuildConfig BindGlobalOptions()
        {
            var section = config.GetSection(nameof(BuildConfig));

            // Helper to parse bools
            static bool? ParseBool(string? value) => bool.TryParse(value, out var b) ? b : null;

            // Helper to parse Enums
            static T? ParseEnum<T>(string? value)
                where T : struct => Enum.TryParse<T>(value, true, out var t) ? t : null;

            return new BuildConfig
            {
                Rid = section[nameof(BuildConfig.Rid)],
                Version = section[nameof(BuildConfig.Version)],
                Target = ParseEnum<BuildTarget>(section[nameof(BuildConfig.Target)]),
                Quick = ParseBool(section[nameof(BuildConfig.Quick)]),
                SkipPreparation = ParseBool(section[nameof(BuildConfig.SkipPreparation)]),
                SkipPackaging = ParseBool(section[nameof(BuildConfig.SkipPackaging)]),
                SkipDelivery = ParseBool(section[nameof(BuildConfig.SkipDelivery)]),
            };
        }

        public NugetConfig BindDeliverySettings()
        {
            var section = config.GetSection(nameof(NugetConfig));
            return new NugetConfig { ApiKey = section[nameof(NugetConfig.ApiKey)] };
        }
    }
}
