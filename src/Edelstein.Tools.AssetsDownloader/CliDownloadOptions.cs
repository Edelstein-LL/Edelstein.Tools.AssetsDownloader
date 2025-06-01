namespace Edelstein.Tools.AssetDownloader;

public class CliDownloadOptions
{
    public string? AssetsHost { get; set; }
    public string? ApiHost { get; set; }
    public string? AssetVersion { get; set; }
    public DownloadScheme DownloadScheme { get; set; }
    public required string[] Languages { get; set; }
    public required DirectoryInfo ExtractedManifestsDirectory { get; set; }
    public required DirectoryInfo DownloadDirectory { get; set; }
    public int ParallelDownloadsCount { get; set; }
    public bool NoAndroid { get; set; }
    public bool NoIos { get; set; }
    public bool NoJsonManifest { get; set; }
    public bool Http { get; set; }
}
