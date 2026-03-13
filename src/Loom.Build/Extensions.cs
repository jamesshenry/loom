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

            // switch (context.Target)
            // {
            // case BuildTarget.Release:
            services.AddModule<VelopackReleaseModule>();
            // break;
            // case BuildTarget.Publish:
            services.AddModule<PublishModule>();
            // break;
            // case BuildTarget.Test:
            services.AddModule<TestModule>();
            // break;
            // case BuildTarget.NugetUpload:
            services.AddModule<NugetUploadModule>();
            // break;
            // case BuildTarget.Clean:
            services.AddModule<CleanModule>();
            // break;
            // case BuildTarget.Build:
            services.AddModule<BuildModule>();
            // break;
            // default:
            // throw new InvalidOperationException($"Unhandled enum value: {context.Target}");
            // }
            return services;
        }
    }
}
