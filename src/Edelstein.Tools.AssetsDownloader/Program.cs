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
        Command downloadCommand = ConfigureDownloadCommand();
        Command restructureCommand = ConfigureRestructureCommand();
        Command decryptCommand = ConfigureDecryptCommand();

        RootCommand rootCommand = new("Edelstein assets downloader")
        {
            downloadCommand,
            restructureCommand,
            decryptCommand
        };

        return await rootCommand.InvokeAsync(args);
    }

    private static Command ConfigureDownloadCommand()
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
        Option<DirectoryInfo> extractedManifestsDirOption = new(["--manifests-out-dir", "-m"],
            () => new DirectoryInfo("manifests"),
            "Directory where extracted manifest files will be located");
        Option<DirectoryInfo> downloadDirOption = new(["--output-dir", "-o"],
            () => new DirectoryInfo("assets"),
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

        Command downloadCommand = new("download", "Downloads assets")
        {
            assetsHostOption,
            apiHostOption,
            downloadSchemeOption,
            languagesOption,
            extractedManifestsDirOption,
            downloadDirOption,
            parallelDownloadsCountOption,
            noAndroidOption,
            noIosOption,
            noJsonManifestOption,
            httpOption
        };
        downloadCommand.AddAlias("d");

        downloadCommand.SetHandler(DownloadAsync,
            new CliDownloadOptionsBinder(assetsHostOption, apiHostOption, downloadSchemeOption, languagesOption,
                extractedManifestsDirOption, downloadDirOption, parallelDownloadsCountOption, noAndroidOption, noIosOption,
                noJsonManifestOption, httpOption));

        return downloadCommand;
    }

    private static Command ConfigureRestructureCommand()
    {
        Option<DirectoryInfo> inputDirOption = new(["--input-dir", "-i"], () => new DirectoryInfo("assets"),
            "Directory with assets files using original directory structure");
        Option<DirectoryInfo> outputDirOption = new(["--output-dir", "-o"], () => new DirectoryInfo("assets-restructured"),
            "Directory where restructured assets will be located");
        Option<bool> noSourcenamesOption = new(["--no-sourcenames"], () => false,
            "Disables generation of .sourcename files that contain information about original file names and locations");

        Command restructureCommand =
            new("restructure",
                "Renames all assets files to human understandable format, combines .ppart and .spart files and recreates directory structure")
            {
                inputDirOption,
                outputDirOption,
                noSourcenamesOption
            };
        restructureCommand.AddAlias("r");

        restructureCommand.SetHandler(AssetsRestructurer.RestructureAsync, inputDirOption, outputDirOption, noSourcenamesOption);

        return restructureCommand;
    }

    private static Command ConfigureDecryptCommand()
    {
        Option<DirectoryInfo> inputDirOption = new(["--input-dir", "-i"], () => new DirectoryInfo("assets-restructured"),
            "Directory with assets files using restructured directory structure");
        Option<DirectoryInfo> outputDirOption = new(["--output-dir", "-o"], () => new DirectoryInfo("assets-decrypted"),
            "Directory where decrypted assets will be located");

        Command decryptCommand =
            new("decrypt",
                "Decrypts sounds and movie files")
            {
                inputDirOption,
                outputDirOption
            };
        decryptCommand.AddAlias("c");

        MediaDecryptor mediaDecryptor = new();

        decryptCommand.SetHandler(mediaDecryptor.DecryptAsync, inputDirOption, outputDirOption);

        return decryptCommand;
    }

    private static async Task DownloadAsync(CliDownloadOptions downloadOptions)
    {
        ServicePointManager.DefaultConnectionLimit = downloadOptions.ParallelDownloadsCount;

        downloadOptions.DownloadDirectory.Create();
        downloadOptions.ExtractedManifestsDirectory.Create();
        downloadOptions.ExtractedManifestsDirectory.CreateSubdirectory(AssetPlatformConverter.ToString(AssetPlatform.Android));
        downloadOptions.ExtractedManifestsDirectory.CreateSubdirectory(AssetPlatformConverter.ToString(AssetPlatform.Ios));

        if (downloadOptions.DownloadScheme is DownloadScheme.Jp)
            await new JpAssetDownloader(downloadOptions).DonwloadAssetsAsync();
        else
            await new GlobalAssetDownloder(downloadOptions).DonwloadAssetsAsync();

        AnsiConsole.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
