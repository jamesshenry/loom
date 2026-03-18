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

            return services;
        }
    }
}
