using Spectre.Console;

using System.CommandLine;
using System.Net;
using System.Text.Json;

namespace Edelstein.Tools.AssetDownloader;

internal class Program
{
    public static readonly JsonSerializerOptions SnakeCaseJsonSerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static readonly JsonSerializerOptions IndentedJsonSerializerOptions =
        new() { WriteIndented = true };

    public static async Task<int> Main(string[] args)
    {
        Option<string?> assetsHostOption = new("--assets-host",
            () => null,
            "Host of asset storage");
        Option<string?> apiHostOption = new("--api-host",
            () => null,
            "Host of game's API");
        Option<DownloadScheme> downloadSchemeOption = new(["--download-scheme", "-s"],
            () => DownloadScheme.Jp,
            "Download scheme used by the tool (Global or Jp)");
        Option<string[]> languagesOption = new(["--languages", "-l"],
            () => ["EN", "ZH", "KR"],
            "Languages to download for Global scheme");
        Option<string> extractedManifestsPathOption = new(["--manifests-out-dir", "-m"],
            () => "manifests",
            "Directory where extracted manifest files will be located");
        Option<string> downloadPathOption = new(["--output-dir", "-o"],
            () => "assets",
            "Directory to which assets should be downloaded");
        Option<int> parallelDownloadsCountOption = new(["--parallel-downloads", "-p"],
            () => 10,
            "Count of parallel downloads");
        Option<bool> noAndroidOption = new("--no-android",
            () => false,
            "Exclude Android assets from download");
        Option<bool> noIosOption = new("--no-ios",
            () => false,
            "Exclude iOS assets from download");
        Option<bool> noJsonManifestOption = new("--no-manifest-json",
            () => false,
            "Exclude generating JSON from manifests");
        Option<bool> httpOption = new("--http",
            () => false,
            "Use plain HTTP instead of HTTPS");

        RootCommand rootCommand = new("Edelstein assets downloader")
        {
            assetsHostOption,
            apiHostOption,
            downloadSchemeOption,
            languagesOption,
            extractedManifestsPathOption,
            downloadPathOption,
            parallelDownloadsCountOption,
            noAndroidOption,
            noIosOption,
            noJsonManifestOption,
            httpOption
        };

        rootCommand.SetHandler(HandleRootCommandAsync,
            new CliOptionsBinder(assetsHostOption, apiHostOption, downloadSchemeOption, languagesOption, extractedManifestsPathOption,
                downloadPathOption, parallelDownloadsCountOption, noAndroidOption, noIosOption, noJsonManifestOption,
                httpOption));

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task HandleRootCommandAsync(CliOptions options)
    {
        ServicePointManager.DefaultConnectionLimit = options.ParallelDownloadsCount;

        Directory.CreateDirectory(options.DownloadPath);
        Directory.CreateDirectory(options.ExtractedManifestsPath);
        Directory.CreateDirectory(Path.Combine(options.ExtractedManifestsPath, AssetPlatformConverter.ToString(AssetPlatform.Android)));
        Directory.CreateDirectory(Path.Combine(options.ExtractedManifestsPath, AssetPlatformConverter.ToString(AssetPlatform.Ios)));

        if (options.DownloadScheme is DownloadScheme.Jp)
            await new JpAssetDownloader(options).DonwloadAssetsAsync();
        else
            await new GlobalAssetDownloder(options).DonwloadAssetsAsync();

        AnsiConsole.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
