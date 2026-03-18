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
    public WorkspaceSettings Workspace { get; set; } = new();

    public Dictionary<string, ArtifactSettings> Artifacts { get; set; } = [];

    public ExecutionOptions Run { get; set; } = new();
}

public class ArtifactSettings
{
    public string Project { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Rid { get; set; }
    public string? Version { get; set; }
    public string? VelopackId { get; internal set; }
}

public class WorkspaceSettings
{
    public string Solution { get; set; } = string.Empty;
    public string ArtifactsPath { get; set; } = ".artifacts";
}

public class ExecutionOptions
{
    public BuildTarget Target { get; set; } = BuildTarget.Build;
    public string? Configuration { get; set; }
    public string? Rid { get; set; }
    public string? Version { get; set; }

    public IEnumerable<KeyValuePair<string, string?>> ToInMemoryCollection()
    {
        var dict = new Dictionary<string, string?>();

        if (Rid != null)
            dict[$"{nameof(LoomSettings.Run)}:{nameof(Rid)}"] = Rid;

        if (Version != null)
            dict[$"{nameof(LoomSettings.Run)}:{nameof(Version)}"] = Version;

        dict[$"{nameof(LoomSettings.Run)}:{nameof(Target)}"] = Target.ToString();

        return dict;
    }
}

public class PackagingSettings
{
    public string? VelopackId { get; set; }

    public string? NugetApiKey { get; set; }
    public string NugetSource { get; set; } = "https://api.nuget.org/v3/index.json";
}
