using AssetsTools.NET;
using AssetsTools.NET.Extra;

using Edelstein.Assets.Management;
using Edelstein.Assets.Management.Manifest;
using Edelstein.Assets.Management.Manifest.Bundle;
using Edelstein.Assets.Management.Manifest.Movie;
using Edelstein.Assets.Management.Manifest.Sound;
using Edelstein.Data.Transport;
using Edelstein.Security;

using Spectre.Console;

using System.Net.Http.Headers;
using System.Text.Json;

namespace Edelstein.Tools.AssetDownloader;

public class GlobalAssetDownloder
{
    private readonly HttpClient _httpClient = new();
    private readonly CliOptions _options;

    private const string DefaultAssetsHost = "img-sif2.lovelive-sif2.com";
    private const string DefaultApiHost = "api-sif2.lovelive-sif2.com";

    private readonly string _assetsBaseUrl;
    private readonly string _apiBaseUrl;

    public GlobalAssetDownloder(CliOptions options)
    {
        _options = options;

        if (_options.Http)
        {
            _assetsBaseUrl = "http://";
            _apiBaseUrl = "http://";
        }
        else
        {
            _assetsBaseUrl = "https://";
            _apiBaseUrl = "https://";
        }

        if (_options.AssetsHost is null)
            _assetsBaseUrl += DefaultAssetsHost;
        else
            _assetsBaseUrl += _options.AssetsHost;

        if (_options.ApiHost is null)
            _apiBaseUrl += DefaultApiHost;
        else
            _apiBaseUrl += _options.ApiHost;
    }

    public async Task DonwloadAssetsAsync()
    {
        AnsiConsole.WriteLine("Retrieving manifest hash...");

        string? androidManifestHash = null,
                iosManifestHash = null;

        if (!_options.NoAndroid)
            androidManifestHash = await RetrieveManifestHashAsync(AssetPlatform.Android);
        if (!_options.NoIos)
            iosManifestHash = await RetrieveManifestHashAsync(AssetPlatform.Ios);

        await using (StreamWriter sw = new(Path.Combine(_options.ExtractedManifestsPath, "hashes.txt"),
            new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.Read
            }))
        {
            if (!_options.NoAndroid)
            {
                AnsiConsole.MarkupLine($"Android manifest hash is [green]{androidManifestHash}[/]");
                await sw.WriteLineAsync($"Android manifest hash is {androidManifestHash}");
            }

            if (!_options.NoIos)
            {
                AnsiConsole.MarkupLine($"iOS manifest hash is [green]{iosManifestHash}[/]");
                await sw.WriteLineAsync($"iOS manifest hash is {iosManifestHash}");
            }
        }

        foreach (string language in _options.Languages)
        {
            Directory.CreateDirectory(Path.Combine(_options.ExtractedManifestsPath, AssetPlatformConverter.ToString(AssetPlatform.Android),
                language));
            Directory.CreateDirectory(Path.Combine(_options.ExtractedManifestsPath, AssetPlatformConverter.ToString(AssetPlatform.Ios),
                language));

            AnsiConsole.WriteLine($"Processing language: {language}");

            AnsiConsole.WriteLine("Downloading manifest...");

            if (!_options.NoAndroid)
            {
                await DownloadManifestAsync(AssetPlatform.Android, language, androidManifestHash!);

                AnsiConsole.WriteLine("Unpacking Android manifest...");

                await UnpackManifestAsync(AssetPlatform.Android, language, androidManifestHash!);
            }

            if (!_options.NoIos)
            {
                await DownloadManifestAsync(AssetPlatform.Ios, language, iosManifestHash!);

                AnsiConsole.WriteLine("Unpacking iOS manifest...");

                await UnpackManifestAsync(AssetPlatform.Ios, language, iosManifestHash!);
            }

            if (!_options.NoAndroid)
            {
                AnsiConsole.WriteLine("Decrypting and deserializing Android manifests...");
                (BundleManifest bundles, MovieManifest movies, SoundManifest sounds) =
                    await DeserializeManifestsAsync(AssetPlatform.Android, language);

                if (!_options.NoJsonManifest)
                {
                    AnsiConsole.WriteLine("Serializing Android manifests to JSON...");
                    await SerializeManifestsToJsonAsync(AssetPlatform.Android, language, bundles, movies, sounds);
                }

                AnsiConsole.WriteLine("Starting Android assets download...");
                await DownloadAssetsAsync(AssetPlatform.Android, language, bundles, movies, sounds);
            }

            if (!_options.NoIos)
            {
                AnsiConsole.WriteLine("Decrypting and deserializing iOS manifests...");
                (BundleManifest bundles, MovieManifest movies, SoundManifest sounds) =
                    await DeserializeManifestsAsync(AssetPlatform.Ios, language);

                if (!_options.NoJsonManifest)
                {
                    AnsiConsole.WriteLine("Serializing iOS manifests to JSON...");
                    await SerializeManifestsToJsonAsync(AssetPlatform.Ios, language, bundles, movies, sounds);
                }

                AnsiConsole.WriteLine("Starting iOS assets download...");
                await DownloadAssetsAsync(AssetPlatform.Ios, language, bundles, movies, sounds);
            }
        }

        AnsiConsole.WriteLine("Download completed!");
    }

    private async Task<string> RetrieveManifestHashAsync(AssetPlatform platform)
    {
        string platformString = AssetPlatformConverter.ToPlayerString(platform);

        string clientData = await PayloadCryptor.EncryptAsync(JsonSerializer.Serialize(new
        {
            asset_version = "0",
            environment = "release"
        }));

        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, $"{_apiBaseUrl}/api/start/assetHash");

        httpRequestMessage.Headers.Add("Aoharu-Platform", platformString);

        httpRequestMessage.Content = new StringContent(clientData, MediaTypeHeaderValue.Parse("application/json"));

        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequestMessage);

        string decryptedData = PayloadCryptor.Decrypt(await response.Content.ReadAsStringAsync());

        return JsonSerializer.Deserialize<ServerResponse<AssetHashResponse>>(decryptedData, Program.SnakeCaseJsonSerializerOptions)
            .Data.AssetHash;
    }

    private async Task DownloadManifestAsync(AssetPlatform platform, string language, string manifestHash)
    {
        string platformString = AssetPlatformConverter.ToString(platform);

        Uri downloadUri =
            new($"{_assetsBaseUrl}/{platformString}/{language}/{manifestHash}/{AssetsConstants.ManifestName}.unity3d");

        await using Stream httpStream = await _httpClient.GetStreamAsync(downloadUri);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(_options.DownloadPath, downloadUri.AbsolutePath[1..]))!);
        await using StreamWriter fileWriter = new(Path.Combine(_options.DownloadPath, downloadUri.AbsolutePath[1..]), false);
        await httpStream.CopyToAsync(fileWriter.BaseStream);
    }

    private async Task UnpackManifestAsync(AssetPlatform platform, string language, string manifestHash)
    {
        string manifestBundleFilePath = Path.Combine(_options.DownloadPath, AssetPlatformConverter.ToString(platform),
            language, manifestHash, $"{AssetsConstants.ManifestName}.unity3d");

        AssetsManager assetsManager = new();
        BundleFileInstance bundleFileInstance = assetsManager.LoadBundleFile(manifestBundleFilePath);
        AssetsFileInstance assetsFileInstance = assetsManager.LoadAssetsFileFromBundle(bundleFileInstance, 0);
        AssetsFile assetsFile = assetsFileInstance.file;

        foreach (AssetFileInfo assetFileInfo in assetsFile.GetAssetsOfType(AssetClassID.TextAsset))
        {
            AssetTypeValueField baseField = assetsManager.GetBaseField(assetsFileInstance, assetFileInfo);

            string unpackedManifestBundleFilePath = Path.Combine(_options.ExtractedManifestsPath,
                AssetPlatformConverter.ToString(platform), language, $"{baseField["m_Name"].AsString}.bytes");

            await using (FileStream sw = File.Open(unpackedManifestBundleFilePath, FileMode.Create, FileAccess.Write))
            {
                await sw.WriteAsync(baseField["m_Script"].AsByteArray);
            }
        }
    }

    private async Task<(BundleManifest Bundles, MovieManifest Movies, SoundManifest Sounds)> DeserializeManifestsAsync(
        AssetPlatform platform, string language)
    {
        string platformString = AssetPlatformConverter.ToString(platform);

        BundleManifest bundles;
        MovieManifest movies;
        SoundManifest sounds;

        await using (FileStream bundlesFileStream = new(
            Path.Combine(_options.ExtractedManifestsPath, platformString, language, "Bundle.bytes"),
            FileMode.Open, FileAccess.Read))
        {
            using MemoryStream decryptedStream = new();
            await ManifestCryptor.DecryptAsync(bundlesFileStream, decryptedStream);
            decryptedStream.Position = 0;
            bundles = BinarySerializer.Deserialize<BundleManifest, ManifestSerializationBinder>(decryptedStream);
        }

        await using (FileStream moviesFileStream = new(
            Path.Combine(_options.ExtractedManifestsPath, platformString, language, "Movie.bytes"),
            FileMode.Open, FileAccess.Read))
        {
            using MemoryStream decryptedStream = new();
            await ManifestCryptor.DecryptAsync(moviesFileStream, decryptedStream);
            decryptedStream.Position = 0;
            movies = BinarySerializer.Deserialize<MovieManifest, ManifestSerializationBinder>(decryptedStream);
        }

        await using (FileStream soundsFileStream = new(
            Path.Combine(_options.ExtractedManifestsPath, platformString, language, "Sound.bytes"),
            FileMode.Open, FileAccess.Read))
        {
            using MemoryStream decryptedStream = new();
            await ManifestCryptor.DecryptAsync(soundsFileStream, decryptedStream);
            decryptedStream.Position = 0;
            sounds = BinarySerializer.Deserialize<SoundManifest, ManifestSerializationBinder>(decryptedStream);
        }

        return (bundles, movies, sounds);
    }

    private async Task SerializeManifestsToJsonAsync(AssetPlatform platform, string language, BundleManifest bundles, MovieManifest movies,
        SoundManifest sounds)
    {
        string platformString = AssetPlatformConverter.ToString(platform);

        await using (StreamWriter sw =
            new(Path.Combine(_options.ExtractedManifestsPath, $"{platformString}_{language}_Bundle.json"), false))
        {
            await sw.WriteAsync(JsonSerializer.Serialize(bundles, Program.IndentedJsonSerializerOptions));
        }

        await using (StreamWriter sw = new(Path.Combine(_options.ExtractedManifestsPath, $"{platformString}_{language}_Movie.json"), false))
        {
            await sw.WriteAsync(JsonSerializer.Serialize(movies, Program.IndentedJsonSerializerOptions));
        }

        await using (StreamWriter sw = new(Path.Combine(_options.ExtractedManifestsPath, $"{platformString}_{language}_Sound.json"), false))
        {
            await sw.WriteAsync(JsonSerializer.Serialize(sounds, Program.IndentedJsonSerializerOptions));
        }
    }

    private async Task DownloadAssetsAsync(AssetPlatform platform, string language, BundleManifest bundles, MovieManifest movies,
        SoundManifest sounds)
    {
        AnsiConsole.WriteLine("Constructing URIs...");

        List<Uri> allFilesUris = ConstructUris(platform, language, bundles, movies, sounds);

        AnsiConsole.WriteLine("Downloading...");

        await AnsiConsole.Progress()
            .AutoClear(true)
            .HideCompleted(true)
            .Columns([
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
            ])
            .StartAsync(async context =>
            {
                SemaphoreSlim semaphoreSlim = new(_options.ParallelDownloadsCount);
                bool isPausedGlobally = false;

                ProgressTask globalProgressTask = context.AddTask("Global progress", true, allFilesUris.Count);

                await Task.WhenAll(allFilesUris.Select(DownloadAsset));

                async Task DownloadAsset(Uri uri)
                {
                    await semaphoreSlim.WaitAsync();

                    // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
                    while (isPausedGlobally)
                        await Task.Delay(100);

                    ProgressTask progressTask = context.AddTask(uri.AbsolutePath);

                    try
                    {
                        using HttpResponseMessage response = await _httpClient.GetAsync(uri);
                        if (!response.IsSuccessStatusCode)
                            throw new Exception();

                        await using Stream httpStream = await response.Content.ReadAsStreamAsync();

                        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(_options.DownloadPath, uri.AbsolutePath[1..]))!);

                        await using StreamWriter fileWriter = new(Path.Combine(_options.DownloadPath, uri.AbsolutePath[1..]), false);

                        await httpStream.CopyToWithProgressAsync(fileWriter.BaseStream, response.Content.Headers.ContentLength,
                            progressTask);
                    }
                    catch (Exception ex)
                    {
                        isPausedGlobally = true;

                        AnsiConsole.WriteException(ex);

                        while (!AnsiConsole.Confirm("Continue?")) { }

                        isPausedGlobally = false;
                    }

                    progressTask.StopTask();
                    globalProgressTask.Increment(1);
                    semaphoreSlim.Release();
                }
            });
    }

    private List<Uri> ConstructUris(AssetPlatform platform, string language, BundleManifest bundles, MovieManifest movies,
        SoundManifest sounds)
    {
        string platformString = AssetPlatformConverter.ToString(platform);

        List<Uri> result = [];

        foreach (BundleManifestEntry entry in bundles.Entries)
            result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{language}/{entry.Hash}/{entry.Name}.unity3d"));

        foreach (MovieManifestEntry entry in movies.Entries)
        {
            if (entry.EnableSplit)
            {
                result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{language}/{entry.Hash}/{entry.Name}.usm.ppart"));
                result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{language}/{entry.Hash}/{entry.Name}.usm.spart"));
            }
            else
                result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{language}/{entry.Hash}/{entry.Name}.usm"));
        }

        foreach (SoundManifestEntry entry in sounds.Entries)
        {
            if (entry.EnableSplit)
            {
                result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{language}/{entry.Hash}/{entry.Name}.acb.ppart"));
                result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{language}/{entry.Hash}/{entry.Name}.acb.spart"));

                if (entry.AwbHash is not "")
                {
                    result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{language}/{entry.Hash}/{entry.Name}.awb.ppart"));
                    result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{language}/{entry.Hash}/{entry.Name}.awb.spart"));
                }
            }
            else
            {
                result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{language}/{entry.Hash}/{entry.Name}.acb"));

                if (entry.AwbHash is not "")
                    result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{language}/{entry.Hash}/{entry.Name}.awb"));
            }
        }

        return result;
    }
}
