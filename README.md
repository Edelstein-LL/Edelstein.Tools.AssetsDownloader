# Edelstein.Tools.AssetsDownloader

Edelstein.Tools.AlbumDownloader is a command-line tool to manipulate assets of Love Live SIF2.

It can:

- Download (both Global and JP) assets
- Restructure all files to original directory structure and names, including merging `.ppart` and `.spart` files
- Decrypt all sounds and movies from restructured directory using [vgmstream](https://github.com/vgmstream/vgmstream) and [FFmpeg](https://git.ffmpeg.org/ffmpeg.git)

## Install

This program requires the [.NET 8.0 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) to run and optionally [vgmstream-cli](https://github.com/vgmstream/vgmstream/releases/latest) and [FFmpeg](https://ffmpeg.org/download.html).

Download respective [latest release](https://github.com/Edelstein-LL/Edelstein.Tools.AssetsDownloader/releases/latest) executable for your OS and architecture.

If you need sounds and movies decryption, download [vgmstream-cli](https://github.com/vgmstream/vgmstream/releases/latest) and [FFmpeg](https://ffmpeg.org/download.html) for your OS and architecture and extract the files to the directory where you downloaded AssetsDownloader. You can also install both `vgmstream-cli` and `FFmpeg` using your OS package manager.

## Usage

```bash
./Edelstein.Tools.AssetsDownloader [command] [options]
```

Every command have respective `--help`/`-h`/`-?` option to display help about the command and its options.

- `download` (`d`) — Downloads assets
  - `--assets-host <assets-host>`                    Host of asset storage []
  - `--api-host <api-host>`                          Host of game's API []
  - `-s, --download-scheme <Global|Jp>`              Download scheme used by the tool (Global or Jp) [default: Jp]
  - `-l, --languages <languages>`                    Languages to download for Global scheme [default: EN|ZH|KR]
  - `-m, --manifests-out-dir <manifests-out-dir>`    Directory where extracted manifest files will be located [default: manifests]
  - `-o, --output-dir <output-dir>`                  Directory to which assets should be downloaded [default: assets]
  - `-p, --parallel-downloads <parallel-downloads>`  Count of parallel downloads [default: 10]
  - `--no-android`                                   Exclude Android assets from download [default: False]
  - `--no-ios`                                       Exclude iOS assets from download [default: False]
  - `--no-manifest-json`                             Exclude generating JSON from manifests [default: False]
  - `--http`                                         Use plain HTTP instead of HTTPS [default: False]
- `restructure` (`r`) — Renames all assets files to human understandable format, combines .ppart and .spart files and recreates directory structure
  - `-i, --input-dir <input-dir>`    Directory with assets files using original directory structure [default: assets]
  - `-o, --output-dir <output-dir>`  Directory where restructured assets will be located [default: assets-restructured]
  - `--no-sourcenames`               Disables generation of .sourcename files that contain information about original file names and locations [default: False]
- `decrypt` (`c`) — Decrypts sounds and movie files
  - `-i, --input-dir <input-dir>`    Directory with assets files using restructured directory structure [default: assets-restructured]
  - `-o, --output-dir <output-dir>`  Directory where decrypted assets will be located [default: assets-decrypted]

## License

See [LICENSE](LICENSE)

## Used libraries

- Edelstein
  - [Edelstein.Assets.Management](https://github.com/Edelstein-LL/Edelstein.Assets.Management)
  - [Edelstein.Assets.Usm](https://github.com/Edelstein-LL/Edelstein.Assets.Usm)
  - [Edelstein.Data](https://github.com/Edelstein-LL/Edelstein.Data)
  - [Edelstein.Security](https://github.com/Edelstein-LL/Edelstein.Security)
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET)
- [Spectre.Console](https://github.com/spectreconsole/spectre.console)
- [System.CommandLine](https://github.com/dotnet/command-line-api)
