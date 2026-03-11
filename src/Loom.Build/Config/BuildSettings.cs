using System.Text.Json.Serialization;

namespace Loom.Config;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
)]
[JsonSerializable(typeof(LoomSettings))]
public partial class LoomSettingsContext : JsonSerializerContext;

public class LoomSettings
{
    public ProjectConfig Project { get; set; } = null!;
    public BuildConfig Build { get; set; } = null!;
    public NugetConfig? Nuget { get; set; } = null!;
}
