using System.Text.Json.Serialization;

namespace Loom.Config;

public class BuildConfig
{
    public string? Rid { get; set; }
    public string? Version { get; set; }
    public BuildTarget? Target { get; set; }
    public bool? Quick { get; set; }

    [JsonIgnore]
    public bool? SkipPreparation { get; set; }

    [JsonIgnore]
    public bool? SkipPackaging { get; set; }

    [JsonIgnore]
    public bool? SkipDelivery { get; set; }

    public IEnumerable<KeyValuePair<string, string?>> ToInMemoryCollection()
    {
        var dict = new Dictionary<string, string?>();

        if (Rid != null)
            dict[$"{nameof(LoomSettings.Build)}:Rid"] = Rid;
        if (Version != null)
            dict[$"{nameof(LoomSettings.Build)}:Version"] = Version;
        if (Target.HasValue)
            dict[$"{nameof(LoomSettings.Build)}:Target"] = Target.ToString();
        if (Quick.HasValue)
            dict[$"{nameof(LoomSettings.Build)}:Quick"] = Quick.Value.ToString();
        if (SkipPreparation.HasValue)
            dict[$"{nameof(LoomSettings.Build)}:SkipPreparation"] =
                SkipPreparation.Value.ToString();
        if (SkipPackaging.HasValue)
            dict[$"{nameof(LoomSettings.Build)}:SkipPackaging"] = SkipPackaging.Value.ToString();
        if (SkipDelivery.HasValue)
            dict[$"{nameof(LoomSettings.Build)}:SkipDelivery"] = SkipDelivery.Value.ToString();

        return dict;
    }
}
