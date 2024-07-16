using Edelstein.Assets.Usm;

using Spectre.Console;

using System.Diagnostics;

namespace Edelstein.Tools.AssetDownloader;

public class MediaDecryptor
{
    private readonly UsmDemuxer _usmDemuxer = new(0x46537c6ceb39d400);

    public async Task<int> DecryptAsync(DirectoryInfo inputDir, DirectoryInfo outputDir)
    {
        outputDir.Create();

        if (!await IsVgmstreamAvailable())
        {
            AnsiConsole.MarkupLine("vgmstream-cli has not been found! " +
                "Have you downloaded it and extracted to program's location? " +
                "Download vgmstream-cli: [link]https://github.com/vgmstream/vgmstream/releases/latest[/]");
            return 1;
        }

        if (!await IsFFmpegAvailable())
        {
            AnsiConsole.MarkupLine("ffmpeg has not been found! " +
                "Have you installed it? " +
                "Download ffmpeg: [link]https://ffmpeg.org/download.html[/]");
            return 1;
        }

        AnsiConsole.Write("Starting decryption...");

        List<string> soundsFilePaths = Directory.EnumerateFiles(inputDir.FullName, "*.acb", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(inputDir.FullName, "*.awb", SearchOption.AllDirectories))
            .ToList();

        List<string> movieFilePaths = Directory.EnumerateFiles(inputDir.FullName, "*.usm", SearchOption.AllDirectories).ToList();

        await AnsiConsole.Progress()
            .HideCompleted(true)
            .AutoClear(true)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
            .StartAsync(async progressContext =>
            {
                ProgressTask globalTask = progressContext.AddTask($"Sounds progress (0/{soundsFilePaths.Count})", true,
                    soundsFilePaths.Count);

                foreach (string filePath in soundsFilePaths)
                {
                    string relativePath = Path.GetRelativePath(inputDir.FullName, filePath);
                    string fileOutputDir = Path.Combine(outputDir.FullName, Path.GetDirectoryName(relativePath)!,
                        Path.GetFileNameWithoutExtension(filePath));
                    Directory.CreateDirectory(fileOutputDir);

                    await DecryptSoundsFile(filePath, fileOutputDir);

                    globalTask.Increment(1);
                    globalTask.Description = $"Sounds progress ({(int)globalTask.Value}/{soundsFilePaths.Count})";
                }

                globalTask.StopTask();
            });

        await AnsiConsole.Progress()
            .HideCompleted(true)
            .AutoClear(true)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
            .StartAsync(async progressContext =>
            {
                ProgressTask globalTask = progressContext.AddTask($"Movies progress (0/{movieFilePaths.Count})", true,
                    movieFilePaths.Count);

                foreach (string filePath in movieFilePaths)
                {
                    string relativePath = Path.GetRelativePath(inputDir.FullName, filePath);
                    string fileOutputDir = Path.Combine(outputDir.FullName, Path.GetDirectoryName(relativePath)!,
                        Path.GetFileNameWithoutExtension(filePath));
                    Directory.CreateDirectory(fileOutputDir);

                    await DecryptMovie(filePath, fileOutputDir);

                    globalTask.Increment(1);
                    globalTask.Description = $"Movies progress ({(int)globalTask.Value}/{movieFilePaths.Count})";
                }

                globalTask.StopTask();
            });

        AnsiConsole.WriteLine("Success!");
        AnsiConsole.WriteLine("Press any key to exit...");
        Console.ReadKey();

        return 0;
    }

    private static async Task<bool> IsVgmstreamAvailable()
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = "vgmstream-cli",
                Arguments = "-h",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Process process = Process.Start(psi)!;

            await process.StandardOutput.ReadToEndAsync();
            await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            return process.ExitCode == 1;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsFFmpegAvailable()
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Process process = Process.Start(psi)!;

            await process.StandardOutput.ReadToEndAsync();
            await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task DecryptSoundsFile(string inputFilePath, string outputDir)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "vgmstream-cli",
            Arguments = $"\"{inputFilePath}\" -o \"{Path.Combine(outputDir, "?n.wav")}\" -S 0",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Process process = Process.Start(psi)!;

        await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            AnsiConsole.MarkupLine("[red]Something went wrong...[/]");
            throw new Exception();
        }
    }

    private async Task DecryptMovie(string inputFilePath, string outputDir)
    {
        DemuxResult demuxResult;

        await using (FileStream input = File.Open(inputFilePath, FileMode.Open, FileAccess.Read))
        {
            demuxResult = await _usmDemuxer.Demux(input, outputDir);
        }

        ProcessStartInfo psi = new()
        {
            FileName = "ffmpeg",
            Arguments =
                $"-i \"{demuxResult.VideoPaths[0]}\" -c:v copy {Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputFilePath) + ".mkv")}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.Arguments = demuxResult.AudioPaths.Count > 0
            ? $"-i \"{demuxResult.VideoPaths[0]}\" -i \"{demuxResult.AudioPaths[0]}\" -c:v copy -c:a flac {Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputFilePath) + ".mkv")}"
            : $"-i \"{demuxResult.VideoPaths[0]}\" -c:v copy {Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputFilePath) + ".mkv")}";

        Process process = Process.Start(psi)!;

        await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            AnsiConsole.MarkupLine("[red]Something went wrong...[/]");
            throw new Exception();
        }
    }
}
