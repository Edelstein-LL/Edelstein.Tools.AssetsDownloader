namespace Edelstein.Tools.AssetDownloader;

public class CliOptions
{
    public required string? AssetsHost { get; set; }
    public required string? ApiHost { get; set; }
    public DownloadScheme DownloadScheme { get; set; }
    public required string[] Languages { get; set; }
    public required string ExtractedManifestsPath { get; set; }
    public required string DownloadPath { get; set; }
    public int ParallelDownloadsCount { get; set; }
    public bool NoAndroid { get; set; }
    public bool NoIos { get; set; }
    public bool NoJsonManifest { get; set; }
    public bool Http { get; set; }
}
