using System.Text.Json.Serialization;

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
