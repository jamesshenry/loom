namespace Loom.MinVer;

public record MinVerVersion
{
    public MinVerVersion(string versionString)
    {
        MinVerMajor = versionString.Split('.')[0];
        MinVerMinor = versionString.Split('.')[1];
        MinVerPatch = versionString.Split('.')[2].Split('-')[0].Split('+')[0];
        MinVerPreRelease = versionString.Split('+')[0].Contains('-')
            ? versionString.Split('+')[0].Split(['-'], count: 2)[1]
            : string.Empty;
        MinVerBuildMetadata = versionString.Contains('+')
            ? versionString.Split(['+'], count: 2)[1]
            : string.Empty;
        AssemblyVersion = $"{MinVerMajor}.0.0.0";
        FileVersion = $"{MinVerMajor}.{MinVerMinor}.{MinVerPatch}.0";
        PackageVersion = versionString;
        Version = versionString;
    }

    public string MinVerMajor { get; }
    public string MinVerMinor { get; }
    public string MinVerPatch { get; }
    public string MinVerPreRelease { get; }
    public string MinVerBuildMetadata { get; }
    public string AssemblyVersion { get; }
    public string FileVersion { get; }
    public string PackageVersion { get; }
    public string Version { get; }
    public static MinVerVersion V1 => new("1.0.0");

    public override string ToString()
    {
        return Version;
    }

    internal static MinVerVersion? From(string version)
    {
        return new MinVerVersion(version);
    }
}
