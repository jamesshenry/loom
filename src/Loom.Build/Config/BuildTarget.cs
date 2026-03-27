using System.Text.Json.Serialization;

namespace Loom.Config;

[JsonConverter(typeof(JsonStringEnumConverter<BuildTarget>))]
public enum BuildTarget
{
    Clean = 1,
    Restore = 2,
    Build = 4,
    Test = 8,
    Publish = 16,
    Release = 32,
}
