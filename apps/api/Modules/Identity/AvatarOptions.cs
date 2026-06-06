namespace PoolPredict.Api.Modules.Identity;

public sealed class AvatarOptions
{
    public const int MaxUploadBytes = 256 * 1024;
    public const int MinDimension = 48;
    public const int MaxDimension = 960;
    public const int OutputDimension = 48;
    public const int WebpQuality = 70;

    public string? StorageDirectory { get; init; }
    public string PublicPath { get; init; } = "/avatars";
}
