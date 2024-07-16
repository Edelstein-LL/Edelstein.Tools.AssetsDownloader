using AssetsTools.NET;
using AssetsTools.NET.Extra;

using Edelstein.Assets.Management;
using Edelstein.Assets.Management.Manifest;
using Edelstein.Assets.Management.Manifest.Bundle;
using Edelstein.Assets.Management.Manifest.Movie;
using Edelstein.Assets.Management.Manifest.Sound;

using Spectre.Console;

using System.Buffers;
using System.Diagnostics;

namespace Edelstein.Tools.AssetDownloader;

public static class AssetsRestructurer
{
    public static async Task RestructureAsync(DirectoryInfo inputDir, DirectoryInfo outputDir, bool noSourcenames = false)
    {
        AnsiConsole.WriteLine("Loading manifests...");

        (BundleManifest bundleManifest, SoundManifest soundManifest, MovieManifest movieManifest) =
            await LoadManifestsAsync(inputDir.FullName);

        AnsiConsole.WriteLine("Manifests loaded!");

        outputDir.Create();
        string bundlesOutputPath = outputDir.CreateSubdirectory("Bundles").FullName;
        string soundsOutputPath = outputDir.CreateSubdirectory("Sounds").FullName;
        string moviesOutputPath = outputDir.CreateSubdirectory("Movies").FullName;

        AnsiConsole.WriteLine("Starting restructuring...");

        await AnsiConsole.Progress()
            .HideCompleted(true)
            .AutoClear(true)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
            .StartAsync(async progressContext =>
            {
                int totalFileCount = bundleManifest.Entries.Length + soundManifest.Entries.Length + movieManifest.Entries.Length;
                ProgressTask globalTask = progressContext.AddTask($"Progress (0/{totalFileCount})", true,
                    totalFileCount);

                foreach (BundleManifestEntry manifestEntry in bundleManifest.Entries)
                {
                    string originalFilePath = Path.Combine(inputDir.FullName, manifestEntry.Hash, $"{manifestEntry.Name}.unity3d");
                    string targetFilePath = Path.Combine([bundlesOutputPath, ..manifestEntry.Identifier.Split("/")]) + ".unity3d";

                    Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);

                    File.Copy(originalFilePath, targetFilePath);

                    if (!noSourcenames)
                        await File.WriteAllTextAsync(targetFilePath + ".sourcename", $"{manifestEntry.Hash}/{manifestEntry.Name}");

                    globalTask.Increment(1);
                    globalTask.Description = $"Progress ({(int)globalTask.Value}/{totalFileCount})";
                }

                foreach (SoundManifestEntry manifestEntry in soundManifest.Entries)
                {
                    string acbOriginalFilePath = Path.Combine(inputDir.FullName, manifestEntry.Hash, $"{manifestEntry.Name}.acb");
                    string acbTargetFilePath = Path.Combine([soundsOutputPath, ..manifestEntry.Identifier.Split("/")]) + ".acb";
                    string awbOriginalFilePath = Path.Combine(inputDir.FullName, manifestEntry.Hash, $"{manifestEntry.Name}.awb");
                    string awbTargetFilePath = Path.Combine([soundsOutputPath, ..manifestEntry.Identifier.Split("/")]) + ".awb";

                    Directory.CreateDirectory(Path.GetDirectoryName(acbTargetFilePath)!);

                    if (manifestEntry.EnableSplit)
                    {
                        {
                            await using FileStream ppartFileStream =
                                File.Open(acbOriginalFilePath + ".ppart", FileMode.Open, FileAccess.Read);
                            await using FileStream spartFileStream =
                                File.Open(acbOriginalFilePath + ".spart", FileMode.Open, FileAccess.Read);

                            await using FileStream outputFileStream = File.Open(acbTargetFilePath, FileMode.Create, FileAccess.Write);

                            await MergePartsAsync(ppartFileStream, spartFileStream, outputFileStream);
                        }

                        if (manifestEntry.AwbHash != "")
                        {
                            await using FileStream ppartFileStream =
                                File.Open(awbOriginalFilePath + ".ppart", FileMode.Open, FileAccess.Read);
                            await using FileStream spartFileStream =
                                File.Open(awbOriginalFilePath + ".spart", FileMode.Open, FileAccess.Read);

                            await using FileStream outputFileStream = File.Open(awbTargetFilePath, FileMode.Create, FileAccess.Write);

                            await MergePartsAsync(ppartFileStream, spartFileStream, outputFileStream);
                        }
                    }
                    else
                    {
                        File.Copy(acbOriginalFilePath, acbTargetFilePath);

                        if (manifestEntry.AwbHash != "")
                            File.Copy(awbOriginalFilePath, awbTargetFilePath);
                    }

                    if (!noSourcenames)
                        await File.WriteAllTextAsync(acbTargetFilePath + ".sourcename", $"{manifestEntry.Hash}/{manifestEntry.Name}");

                    globalTask.Increment(1);
                    globalTask.Description = $"Progress ({(int)globalTask.Value}/{totalFileCount})";
                }

                foreach (MovieManifestEntry manifestEntry in movieManifest.Entries)
                {
                    string originalFilePath = Path.Combine(inputDir.FullName, manifestEntry.Hash, $"{manifestEntry.Name}.usm");
                    string targetFilePath = Path.Combine([moviesOutputPath, ..manifestEntry.Identifier.Split("/")]) + ".usm";

                    Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);

                    if (manifestEntry.EnableSplit)
                    {
                        await using FileStream ppartFileStream =
                            File.Open(originalFilePath + ".ppart", FileMode.Open, FileAccess.Read);
                        await using FileStream spartFileStream =
                            File.Open(originalFilePath + ".spart", FileMode.Open, FileAccess.Read);

                        await using FileStream outputFileStream = File.Open(targetFilePath, FileMode.Create, FileAccess.Write);

                        await MergePartsAsync(ppartFileStream, spartFileStream, outputFileStream);
                    }
                    else
                        File.Copy(originalFilePath, targetFilePath);

                    if (!noSourcenames)
                        await File.WriteAllTextAsync(targetFilePath + ".sourcename", $"{manifestEntry.Hash}/{manifestEntry.Name}");

                    globalTask.Increment(1);
                    globalTask.Description = $"Progress ({(int)globalTask.Value}/{totalFileCount})";
                }

                globalTask.StopTask();
            });

        AnsiConsole.WriteLine("Success!");
        AnsiConsole.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    private static async Task MergePartsAsync(Stream ppartStream, Stream spartStream, Stream outputStream)
    {
        const int primaryChunkLength = 1023;
        const int secondaryChunkLength = 1;

        byte[] primaryBuffer = ArrayPool<byte>.Shared.Rent(primaryChunkLength);
        byte[] secondaryBuffer = ArrayPool<byte>.Shared.Rent(secondaryChunkLength);

        while (await spartStream.ReadAsync(secondaryBuffer.AsMemory(0, secondaryChunkLength)) > 0)
        {
            int primaryReadBytesCount = await ppartStream.ReadAsync(primaryBuffer.AsMemory(0, primaryChunkLength));

            Debug.Assert(primaryReadBytesCount > 0);

            await outputStream.WriteAsync(primaryBuffer.AsMemory(0, primaryReadBytesCount));
            await outputStream.WriteAsync(secondaryBuffer.AsMemory(0, secondaryChunkLength));
        }

        ArrayPool<byte>.Shared.Return(primaryBuffer);
        ArrayPool<byte>.Shared.Return(secondaryBuffer);
    }

    private static async Task<(BundleManifest bundleManifest, SoundManifest soundManifest, MovieManifest movieManifest)>
        LoadManifestsAsync(string inputDir)
    {
        string? foundManifestBundle =
            Directory.EnumerateFiles(inputDir, $"{AssetsConstants.ManifestName}.unity3d", SearchOption.AllDirectories)
                .FirstOrDefault();

        string manifestBundleFilePath = foundManifestBundle ?? throw new Exception("Manifest asset bundle has not been found");

        AssetsManager manifestAssetsManager = new();
        BundleFileInstance? manifestFileInstance = manifestAssetsManager.LoadBundleFile(manifestBundleFilePath);
        AssetsFileInstance? manifestAssetsFileInstance = manifestAssetsManager.LoadAssetsFileFromBundle(manifestFileInstance, 0);
        AssetsFile assetsFile = manifestAssetsFileInstance.file;

        byte[]? binaryBundleManifest = null;
        byte[]? binarySoundManifest = null;
        byte[]? binaryMovieManifest = null;

        foreach (AssetFileInfo assetFileInfo in assetsFile.GetAssetsOfType(AssetClassID.TextAsset))
        {
            AssetTypeValueField baseField = manifestAssetsManager.GetBaseField(manifestAssetsFileInstance, assetFileInfo);

            switch (baseField["m_Name"].AsString)
            {
                case "Bundle":
                    binaryBundleManifest = baseField["m_Script"].AsByteArray;
                    break;
                case "Sound":
                    binarySoundManifest = baseField["m_Script"].AsByteArray;
                    break;
                case "Movie":
                    binaryMovieManifest = baseField["m_Script"].AsByteArray;
                    break;
            }
        }

        if (binaryBundleManifest is null)
            throw new Exception("Bundle manifest was not found");

        if (binarySoundManifest is null)
            throw new Exception("Sound manifest was not found");

        if (binaryMovieManifest is null)
            throw new Exception("Movie manifest was not found");

        BundleManifest bundleManifest;
        SoundManifest soundManifest;
        MovieManifest movieManifest;

        using (MemoryStream encryptedStream = new(binaryBundleManifest))
        {
            using (MemoryStream decryptedStream = new())
            {
                await ManifestCryptor.DecryptAsync(encryptedStream, decryptedStream);

                decryptedStream.Position = 0;

                bundleManifest = BinarySerializer.Deserialize<BundleManifest, ManifestSerializationBinder>(decryptedStream);
            }
        }

        using (MemoryStream encryptedStream = new(binarySoundManifest))
        {
            using (MemoryStream decryptedStream = new())
            {
                await ManifestCryptor.DecryptAsync(encryptedStream, decryptedStream);

                decryptedStream.Position = 0;

                soundManifest = BinarySerializer.Deserialize<SoundManifest, ManifestSerializationBinder>(decryptedStream);
            }
        }

        using (MemoryStream encryptedStream = new(binaryMovieManifest))
        {
            using (MemoryStream decryptedStream = new())
            {
                await ManifestCryptor.DecryptAsync(encryptedStream, decryptedStream);

                decryptedStream.Position = 0;

                movieManifest = BinarySerializer.Deserialize<MovieManifest, ManifestSerializationBinder>(decryptedStream);
            }
        }

        return (bundleManifest, soundManifest, movieManifest);
    }
}
