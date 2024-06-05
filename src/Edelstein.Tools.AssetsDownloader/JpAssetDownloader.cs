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

public class JpAssetDownloader
{
    private readonly HttpClient _httpClient = new();
    private readonly CliDownloadOptions _downloadOptions;

    private const string DefaultAssetsHost = "lovelive-schoolidolfestival2-assets.akamaized.net";
    private const string DefaultApiHost = "api.app.lovelive-sif2.bushimo.jp";

    private readonly string _assetsBaseUrl;
    private readonly string _apiBaseUrl;

    public JpAssetDownloader(CliDownloadOptions downloadOptions)
    {
        _downloadOptions = downloadOptions;

        if (_downloadOptions.Http)
        {
            _assetsBaseUrl = "http://";
            _apiBaseUrl = "http://";
        }
        else
        {
            _assetsBaseUrl = "https://";
            _apiBaseUrl = "https://";
        }

        if (_downloadOptions.AssetsHost is null)
            _assetsBaseUrl += DefaultAssetsHost;
        else
            _assetsBaseUrl += _downloadOptions.AssetsHost;

        if (_downloadOptions.ApiHost is null)
            _apiBaseUrl += DefaultApiHost;
        else
            _apiBaseUrl += _downloadOptions.ApiHost;
    }

    public async Task DonwloadAssetsAsync()
    {
        AnsiConsole.WriteLine("Retrieving manifest hash...");

        string? androidManifestHash = null,
                iosManifestHash = null;

        if (!_downloadOptions.NoAndroid)
            androidManifestHash = await RetrieveManifestHashAsync(AssetPlatform.Android);
        if (!_downloadOptions.NoIos)
            iosManifestHash = await RetrieveManifestHashAsync(AssetPlatform.Ios);

        await using (StreamWriter sw = new(Path.Combine(_downloadOptions.ExtractedManifestsPath, "hashes.txt"),
            new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.Read
            }))
        {
            if (!_downloadOptions.NoAndroid)
            {
                AnsiConsole.MarkupLine($"Android manifest hash is [green]{androidManifestHash}[/]");
                await sw.WriteLineAsync($"Android manifest hash is {androidManifestHash}");
            }

            if (!_downloadOptions.NoIos)
            {
                AnsiConsole.MarkupLine($"iOS manifest hash is [green]{iosManifestHash}[/]");
                await sw.WriteLineAsync($"iOS manifest hash is {iosManifestHash}");
            }
        }

        AnsiConsole.WriteLine("Downloading manifest...");

        if (!_downloadOptions.NoAndroid)
        {
            await DownloadManifestAsync(AssetPlatform.Android, androidManifestHash!);

            AnsiConsole.WriteLine("Unpacking Android manifest...");

            string manifestBundleFilePath = Path.Combine(_downloadOptions.DownloadPath, AssetPlatformConverter.ToString(AssetPlatform.Android),
                androidManifestHash!, $"{AssetsConstants.ManifestName}.unity3d");

            AssetsManager assetsManager = new();
            BundleFileInstance bundleFileInstance = assetsManager.LoadBundleFile(manifestBundleFilePath);
            AssetsFileInstance assetsFileInstance = assetsManager.LoadAssetsFileFromBundle(bundleFileInstance, 0);
            AssetsFile assetsFile = assetsFileInstance.file;

            foreach (AssetFileInfo assetFileInfo in assetsFile.GetAssetsOfType(AssetClassID.TextAsset))
            {
                AssetTypeValueField baseField = assetsManager.GetBaseField(assetsFileInstance, assetFileInfo);

                string unpackedManifestBundleFilePath = Path.Combine(_downloadOptions.ExtractedManifestsPath,
                    AssetPlatformConverter.ToString(AssetPlatform.Android), $"{baseField["m_Name"].AsString}.bytes");

                await using FileStream sw = File.Open(unpackedManifestBundleFilePath, FileMode.Create, FileAccess.Write);
                await sw.WriteAsync(baseField["m_Script"].AsByteArray);
            }
        }

        if (!_downloadOptions.NoIos)
        {
            await DownloadManifestAsync(AssetPlatform.Ios, iosManifestHash!);

            AnsiConsole.WriteLine("Unpacking iOS manifest...");

            string manifestBundleFilePath = Path.Combine(_downloadOptions.DownloadPath, AssetPlatformConverter.ToString(AssetPlatform.Ios),
                iosManifestHash!, $"{AssetsConstants.ManifestName}.unity3d");

            AssetsManager assetsManager = new();
            BundleFileInstance bundleFileInstance = assetsManager.LoadBundleFile(manifestBundleFilePath);
            AssetsFileInstance assetsFileInstance = assetsManager.LoadAssetsFileFromBundle(bundleFileInstance, 0);
            AssetsFile assetsFile = assetsFileInstance.file;

            foreach (AssetFileInfo assetFileInfo in assetsFile.GetAssetsOfType(AssetClassID.TextAsset))
            {
                AssetTypeValueField baseField = assetsManager.GetBaseField(assetsFileInstance, assetFileInfo);

                string unpackedManifestBundleFilePath = Path.Combine(_downloadOptions.ExtractedManifestsPath,
                    AssetPlatformConverter.ToString(AssetPlatform.Ios), $"{baseField["m_Name"]}.bytes");

                await using FileStream sw = File.Open(unpackedManifestBundleFilePath, FileMode.Create, FileAccess.Write);
                await sw.WriteAsync(baseField["m_Script"].AsByteArray);
            }
        }

        if (!_downloadOptions.NoAndroid)
        {
            AnsiConsole.WriteLine("Decrypting and deserializing Android manifests...");
            (BundleManifest bundles, MovieManifest movies, SoundManifest sounds) = await DeserializeManifestsAsync(AssetPlatform.Android);

            if (!_downloadOptions.NoJsonManifest)
            {
                AnsiConsole.WriteLine("Serializing Android manifests to JSON...");
                await SerializeManifestsToJsonAsync(AssetPlatform.Android, bundles, movies, sounds);
            }

            AnsiConsole.WriteLine("Starting Android assets download...");
            await DownloadAssetsAsync(AssetPlatform.Android, bundles, movies, sounds);
        }

        if (!_downloadOptions.NoIos)
        {
            AnsiConsole.WriteLine("Decrypting and deserializing iOS manifests...");
            (BundleManifest bundles, MovieManifest movies, SoundManifest sounds) = await DeserializeManifestsAsync(AssetPlatform.Ios);

            if (!_downloadOptions.NoJsonManifest)
            {
                AnsiConsole.WriteLine("Serializing iOS manifests to JSON...");
                await SerializeManifestsToJsonAsync(AssetPlatform.Ios, bundles, movies, sounds);
            }

            AnsiConsole.WriteLine("Starting iOS assets download...");
            await DownloadAssetsAsync(AssetPlatform.Ios, bundles, movies, sounds);
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

    private async Task DownloadManifestAsync(AssetPlatform platform, string manifestHash)
    {
        string platformString = AssetPlatformConverter.ToString(platform);

        Uri downloadUri = new($"{_assetsBaseUrl}/{platformString}/{manifestHash}/{AssetsConstants.ManifestName}.unity3d");

        await using Stream httpStream = await _httpClient.GetStreamAsync(downloadUri);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(_downloadOptions.DownloadPath, downloadUri.AbsolutePath[1..]))!);
        await using StreamWriter fileWriter = new(Path.Combine(_downloadOptions.DownloadPath, downloadUri.AbsolutePath[1..]), false);
        await httpStream.CopyToAsync(fileWriter.BaseStream);
    }

    private async Task<(BundleManifest Bundles, MovieManifest Movies, SoundManifest Sounds)> DeserializeManifestsAsync(
        AssetPlatform platform)
    {
        string platformString = AssetPlatformConverter.ToString(platform);

        BundleManifest bundles;
        MovieManifest movies;
        SoundManifest sounds;

        await using (FileStream bundlesFileStream = new(Path.Combine(_downloadOptions.ExtractedManifestsPath, platformString, "Bundle.bytes"),
            FileMode.Open, FileAccess.Read))
        {
            using MemoryStream decryptedStream = new();
            await ManifestCryptor.DecryptAsync(bundlesFileStream, decryptedStream);
            decryptedStream.Position = 0;
            bundles = BinarySerializer.Deserialize<BundleManifest, ManifestSerializationBinder>(decryptedStream);
        }

        await using (FileStream moviesFileStream = new(Path.Combine(_downloadOptions.ExtractedManifestsPath, platformString, "Movie.bytes"),
            FileMode.Open, FileAccess.Read))
        {
            using MemoryStream decryptedStream = new();
            await ManifestCryptor.DecryptAsync(moviesFileStream, decryptedStream);
            decryptedStream.Position = 0;
            movies = BinarySerializer.Deserialize<MovieManifest, ManifestSerializationBinder>(decryptedStream);
        }

        await using (FileStream soundsFileStream = new(Path.Combine(_downloadOptions.ExtractedManifestsPath, platformString, "Sound.bytes"),
            FileMode.Open, FileAccess.Read))
        {
            using MemoryStream decryptedStream = new();
            await ManifestCryptor.DecryptAsync(soundsFileStream, decryptedStream);
            decryptedStream.Position = 0;
            sounds = BinarySerializer.Deserialize<SoundManifest, ManifestSerializationBinder>(decryptedStream);
        }

        return (bundles, movies, sounds);
    }

    private async Task SerializeManifestsToJsonAsync(AssetPlatform platform, BundleManifest bundles, MovieManifest movies,
        SoundManifest sounds)
    {
        string platformString = AssetPlatformConverter.ToString(platform);

        await using (StreamWriter sw = new(Path.Combine(_downloadOptions.ExtractedManifestsPath, $"{platformString}Bundle.json"), false))
        {
            await sw.WriteAsync(JsonSerializer.Serialize(bundles, Program.IndentedJsonSerializerOptions));
        }

        await using (StreamWriter sw = new(Path.Combine(_downloadOptions.ExtractedManifestsPath, $"{platformString}Movie.json"), false))
        {
            await sw.WriteAsync(JsonSerializer.Serialize(movies, Program.IndentedJsonSerializerOptions));
        }

        await using (StreamWriter sw = new(Path.Combine(_downloadOptions.ExtractedManifestsPath, $"{platformString}Sound.json"), false))
        {
            await sw.WriteAsync(JsonSerializer.Serialize(sounds, Program.IndentedJsonSerializerOptions));
        }
    }

    private async Task DownloadAssetsAsync(AssetPlatform platform, BundleManifest bundles, MovieManifest movies, SoundManifest sounds)
    {
        AnsiConsole.WriteLine("Constructing URIs...");

        List<Uri> allFilesUris = ConstructUris(platform, bundles, movies, sounds);

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
                SemaphoreSlim semaphoreSlim = new(_downloadOptions.ParallelDownloadsCount);
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

                        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(_downloadOptions.DownloadPath, uri.AbsolutePath[1..]))!);

                        await using StreamWriter fileWriter = new(Path.Combine(_downloadOptions.DownloadPath, uri.AbsolutePath[1..]), false);

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

    private List<Uri> ConstructUris(AssetPlatform platform, BundleManifest bundles, MovieManifest movies, SoundManifest sounds)
    {
        string platformString = AssetPlatformConverter.ToString(platform);

        List<Uri> result = [];

        foreach (BundleManifestEntry entry in bundles.Entries)
            result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{entry.Hash}/{entry.Name}.unity3d"));

        foreach (MovieManifestEntry entry in movies.Entries)
        {
            if (entry.EnableSplit)
            {
                result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{entry.Hash}/{entry.Name}.usm.ppart"));
                result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{entry.Hash}/{entry.Name}.usm.spart"));
            }
            else
                result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{entry.Hash}/{entry.Name}.usm"));
        }

        foreach (SoundManifestEntry entry in sounds.Entries)
        {
            if (entry.EnableSplit)
            {
                result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{entry.Hash}/{entry.Name}.acb.ppart"));
                result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{entry.Hash}/{entry.Name}.acb.spart"));

                if (entry.AwbHash is not "")
                {
                    result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{entry.Hash}/{entry.Name}.awb.ppart"));
                    result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{entry.Hash}/{entry.Name}.awb.spart"));
                }
            }
            else
            {
                result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{entry.Hash}/{entry.Name}.acb"));

                if (entry.AwbHash is not "")
                    result.Add(new Uri($"{_assetsBaseUrl}/{platformString}/{entry.Hash}/{entry.Name}.awb"));
            }
        }

        return result;
    }
}
