namespace Edelstein.Tools.AssetDownloader;

public static class AssetPlatformConverter
{
    public static string ToPlayerString(AssetPlatform platform) =>
        platform switch
        {
            AssetPlatform.Android => "Android",
            AssetPlatform.Ios => "IPhonePlayer",
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };

    public static string ToString(AssetPlatform platform) =>
        platform switch
        {
            AssetPlatform.Android => "Android",
            AssetPlatform.Ios => "iOS",
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };
}
