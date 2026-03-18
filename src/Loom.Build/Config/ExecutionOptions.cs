namespace Loom.Config;

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
