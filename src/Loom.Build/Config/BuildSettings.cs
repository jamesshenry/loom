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
    // 1. Persistent Project Definitions (lives in loom.json)
    public WorkspaceSettings Workspace { get; set; } = new();

    // 2. Integration & Distribution Settings (lives in loom.json)
    public PackagingSettings Packaging { get; set; } = new();

    // 3. Transient Run Flags (passed via CLI / Environment)
    public ExecutionOptions Run { get; set; } = new();
}

public class WorkspaceSettings
{
    public string Solution { get; set; } = string.Empty;
    public string MainProject { get; set; } = string.Empty;
    public string ArtifactsPath { get; set; } = ".artifacts"; // Broader term than 'dist'
    public string DefaultVersionPrefix { get; set; } = "1.0.0"; // Base version before CI suffix
}

public class PackagingSettings
{
    public string? VelopackId { get; set; }

    // API keys shouldn't be in JSON, but we leave a slot here for IConfiguration to bind
    // from the LOOM_PACKAGING__NUGETAPIKEY environment variable
    public string? NugetApiKey { get; set; }
    public string NugetSource { get; set; } = "https://api.nuget.org/v3/index.json";
}

public class ExecutionOptions
{
    // Standard Execution
    public BuildTarget Target { get; set; } = BuildTarget.Build;
    public string? Configuration { get; set; } // Can be explicitly passed, otherwise inferred
    public string? Rid { get; set; } // Environment default applied later
    public string? Version { get; set; } // E.g., 1.0.0-ci.123

    // Run Modifiers
    public bool FastMode { get; set; } // Replaces 'Quick'
    public string[] Skip { get; set; } = Array.Empty<string>(); // Flexible skipping
}
