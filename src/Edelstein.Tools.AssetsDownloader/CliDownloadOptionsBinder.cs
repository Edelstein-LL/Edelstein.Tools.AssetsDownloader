using System.CommandLine;
using System.CommandLine.Binding;

namespace Edelstein.Tools.AssetDownloader;

public class CliDownloadOptionsBinder : BinderBase<CliDownloadOptions>
{
    private readonly Option<string?> _assetsHostOption;
    private readonly Option<string?> _apiHostOption;
    private readonly Option<string?> _assetVersionOption;
    private readonly Option<DownloadScheme> _downloadScheme;
    private readonly Option<DirectoryInfo> _extractedManifestsDirOption;
    private readonly Option<DirectoryInfo> _downloadDirOption;
    private readonly Option<int> _parallelDownloadsCountOption;
    private readonly Option<bool> _noAndroidOption;
    private readonly Option<bool> _noIosOption;
    private readonly Option<bool> _noJsonManifestOption;
    private readonly Option<bool> _httpOption;
    private readonly Option<string[]> _languagesOption;

    public CliDownloadOptionsBinder(Option<string?> assetsHostOption, Option<string?> apiHostOption, Option<string?> assetVersionOption,
        Option<DownloadScheme> downloadScheme, Option<string[]> languagesOption,
        Option<DirectoryInfo> extractedManifestsDirOption, Option<DirectoryInfo> downloadDirOption,
        Option<int> parallelDownloadsCountOption, Option<bool> noAndroidOption, Option<bool> noIosOption, Option<bool> noJsonManifestOption,
        Option<bool> httpOption)
    {
        _assetsHostOption = assetsHostOption;
        _apiHostOption = apiHostOption;
        _assetVersionOption = assetVersionOption;
        _downloadScheme = downloadScheme;
        _languagesOption = languagesOption;
        _extractedManifestsDirOption = extractedManifestsDirOption;
        _downloadDirOption = downloadDirOption;
        _parallelDownloadsCountOption = parallelDownloadsCountOption;
        _noAndroidOption = noAndroidOption;
        _noIosOption = noIosOption;
        _noJsonManifestOption = noJsonManifestOption;
        _httpOption = httpOption;
    }

    protected override CliDownloadOptions GetBoundValue(BindingContext bindingContext) =>
        new()
        {
            AssetsHost = bindingContext.ParseResult.GetValueForOption(_assetsHostOption),
            ApiHost = bindingContext.ParseResult.GetValueForOption(_apiHostOption),
            AssetVersion = bindingContext.ParseResult.GetValueForOption(_assetVersionOption),
            DownloadScheme = bindingContext.ParseResult.GetValueForOption(_downloadScheme),
            Languages = bindingContext.ParseResult.GetValueForOption(_languagesOption) ?? throw new InvalidOperationException(),
            ExtractedManifestsDirectory =
                bindingContext.ParseResult.GetValueForOption(_extractedManifestsDirOption) ?? throw new InvalidOperationException(),
            DownloadDirectory = bindingContext.ParseResult.GetValueForOption(_downloadDirOption) ?? throw new InvalidOperationException(),
            ParallelDownloadsCount = bindingContext.ParseResult.GetValueForOption(_parallelDownloadsCountOption),
            NoAndroid = bindingContext.ParseResult.GetValueForOption(_noAndroidOption),
            NoIos = bindingContext.ParseResult.GetValueForOption(_noIosOption),
            NoJsonManifest = bindingContext.ParseResult.GetValueForOption(_noJsonManifestOption),
            Http = bindingContext.ParseResult.GetValueForOption(_httpOption)
        };
}
