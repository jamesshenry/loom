using System.Text.Json.Serialization;

namespace Loom.Config;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(LoomSettings))]
public partial class LoomSettingsContext : JsonSerializerContext;

public class LoomSettings
{
    public WorkspaceSettings Workspace { get; set; } = new();

    public Dictionary<string, ArtifactSettings> Artifacts { get; set; } = [];

    public ExecutionOptions Run { get; set; } = new();
    public NugetSettings Nuget { get; set; } = new();
    public string GithubAccessToken { get; set; } = string.Empty;
}

public class NugetSettings
{
    public string ApiKey { get; init; } = string.Empty;
}
