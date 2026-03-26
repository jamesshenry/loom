using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModularPipelines.Engine;

namespace Loom.Velopack;

[ExcludeFromCodeCoverage]
public static class VelopackExtensions
{
#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    [ModuleInitializer]
#pragma warning restore CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    public static void RegisterVelopackContext() =>
        ModularPipelinesContextRegistry.RegisterContext(RegisterVelopackContext);

    public static void RegisterVelopackContext(this IServiceCollection services)
    {
        services.TryAddScoped<IVelopackPack, VelopackPack>();
    }

    public static IVelopackPack Velopack(this IPipelineContext context) =>
        context.Services.Get<IVelopackPack>();
}
