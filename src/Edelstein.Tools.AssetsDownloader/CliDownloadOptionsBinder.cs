using System.CommandLine;
using System.CommandLine.Binding;

namespace Edelstein.Tools.AssetDownloader;

public class CliDownloadOptionsBinder : BinderBase<CliDownloadOptions>
{
    private readonly Option<string?> _assetsHostOption;
    private readonly Option<string?> _apiHostOption;
    private readonly Option<DownloadScheme> _downloadScheme;
    private readonly Option<string> _extractedManifestsPathOption;
    private readonly Option<string> _downloadPathOption;
    private readonly Option<int> _parallelDownloadsCountOption;
    private readonly Option<bool> _noAndroidOption;
    private readonly Option<bool> _noIosOption;
    private readonly Option<bool> _noJsonManifestOption;
    private readonly Option<bool> _httpOption;
    private readonly Option<string[]> _languagesOption;

    public CliDownloadOptionsBinder(Option<string?> assetsHostOption, Option<string?> apiHostOption, Option<DownloadScheme> downloadScheme,
        Option<string[]> languagesOption,
        Option<string> extractedManifestsPathOption, Option<string> downloadPathOption,
        Option<int> parallelDownloadsCountOption, Option<bool> noAndroidOption, Option<bool> noIosOption, Option<bool> noJsonManifestOption,
        Option<bool> httpOption)
    {
        _assetsHostOption = assetsHostOption;
        _apiHostOption = apiHostOption;
        _downloadScheme = downloadScheme;
        _languagesOption = languagesOption;
        _extractedManifestsPathOption = extractedManifestsPathOption;
        _downloadPathOption = downloadPathOption;
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
            DownloadScheme = bindingContext.ParseResult.GetValueForOption(_downloadScheme),
            Languages = bindingContext.ParseResult.GetValueForOption(_languagesOption) ?? throw new InvalidOperationException(),
            ExtractedManifestsPath =
                bindingContext.ParseResult.GetValueForOption(_extractedManifestsPathOption) ?? throw new InvalidOperationException(),
            DownloadPath = bindingContext.ParseResult.GetValueForOption(_downloadPathOption) ?? throw new InvalidOperationException(),
            ParallelDownloadsCount = bindingContext.ParseResult.GetValueForOption(_parallelDownloadsCountOption),
            NoAndroid = bindingContext.ParseResult.GetValueForOption(_noAndroidOption),
            NoIos = bindingContext.ParseResult.GetValueForOption(_noIosOption),
            NoJsonManifest = bindingContext.ParseResult.GetValueForOption(_noJsonManifestOption),
            Http = bindingContext.ParseResult.GetValueForOption(_httpOption)
        };
}
