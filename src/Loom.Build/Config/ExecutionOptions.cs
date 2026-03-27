namespace Loom.Config;

public class GlobalSettings
{
    public BuildTarget Target { get; set; } = BuildTarget.Build;
    public string? Configuration { get; set; }
    public string? Rid { get; set; }
    public string? Version { get; set; }

    public IEnumerable<KeyValuePair<string, string?>> ToInMemoryCollection()
    {
        var dict = new Dictionary<string, string?>();

        if (Rid != null)
            dict[$"{nameof(LoomSettings.Global)}:{nameof(Rid)}"] = Rid;

        if (Version != null)
            dict[$"{nameof(LoomSettings.Global)}:{nameof(Version)}"] = Version;

        dict[$"{nameof(LoomSettings.Global)}:{nameof(Target)}"] = Target.ToString();

        return dict;
    }
}
