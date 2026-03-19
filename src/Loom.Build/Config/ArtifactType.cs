using System.Text.Json.Serialization;

namespace Loom.Config;

[JsonConverter(typeof(JsonStringEnumConverter<ArtifactType>))]
public enum ArtifactType
{
    Nuget,
    Executable,
    Velopack,
}
