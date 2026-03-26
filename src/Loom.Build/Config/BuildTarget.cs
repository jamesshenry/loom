using System.Text.Json.Serialization;

namespace Loom.Config;

[JsonConverter(typeof(JsonStringEnumConverter<BuildTarget>))]
public enum BuildTarget
{
    Clean,
    Restore,
    Build,
    Test,
    Publish,
    Release,
}
