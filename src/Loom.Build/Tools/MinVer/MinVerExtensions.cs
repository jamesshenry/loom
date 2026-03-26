using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModularPipelines.Engine;

namespace Loom.MinVer;

[ExcludeFromCodeCoverage]
public static class MinVerExtensions
{
#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    [ModuleInitializer]
#pragma warning restore CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    public static void RegisterMinVerContext() =>
        ModularPipelinesContextRegistry.RegisterContext(RegisterMinVerContext);

    public static void RegisterMinVerContext(this IServiceCollection services)
    {
        services.TryAddScoped<IMinVer, MinVer>();
    }

    public static IMinVer MinVer(this IPipelineContext context) => context.Services.Get<IMinVer>();
}
