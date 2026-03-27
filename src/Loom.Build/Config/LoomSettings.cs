using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Loom.Config;

#pragma warning disable RCS1060 // Declare each type in separate file

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

    [Description("Global loom settings to use if artifact specifc are not set")]
    public GlobalSettings Global { get; set; } = new();

    [Description(
        "Settings to use NugetUploadModule locally. Use environment variables in production"
    )]
    public NugetSettings Nuget { get; set; } = new();

    [Description("Should not be set in dev")]
    public string GithubAccessToken { get; set; } = string.Empty;
}

public class NugetSettings
{
    public string ApiKey { get; init; } = string.Empty;
}
