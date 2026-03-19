using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

[assembly: InternalsVisibleTo("Loom.Build.Tests")]

namespace Loom.Config;

[JsonConverter(typeof(JsonStringEnumConverter<BuildTarget>))]
public enum BuildTarget
{
    Build,
    Test,
    Publish,
    Restore,
    Release,
    NugetUpload,
    Clean,
}
