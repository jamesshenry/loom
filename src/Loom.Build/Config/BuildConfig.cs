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
            dict[$"{nameof(LoomContext.Build)}:Rid"] = Rid;
        if (Version != null)
            dict[$"{nameof(LoomContext.Build)}:Version"] = Version;
        if (Target.HasValue)
            dict[$"{nameof(LoomContext.Build)}:Target"] = Target.ToString();
        if (Quick.HasValue)
            dict[$"{nameof(LoomContext.Build)}:Quick"] = Quick.Value.ToString();
        if (SkipPreparation.HasValue)
            dict[$"{nameof(LoomContext.Build)}:SkipPreparation"] = SkipPreparation.Value.ToString();
        if (SkipPackaging.HasValue)
            dict[$"{nameof(LoomContext.Build)}:SkipPackaging"] = SkipPackaging.Value.ToString();
        if (SkipDelivery.HasValue)
            dict[$"{nameof(LoomContext.Build)}:SkipDelivery"] = SkipDelivery.Value.ToString();

        return dict;
    }
}
