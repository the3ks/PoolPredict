using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace PoolPredict.Api.Modules.Identity;

public sealed class AvatarService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly AvatarOptions _options;

    public AvatarService(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
        _options = configuration.GetSection("AvatarStorage").Get<AvatarOptions>() ?? new AvatarOptions();
    }

    public async Task<string> SaveAvatarAsync(Guid userId, IFormFile file, CancellationToken cancellationToken)
    {
        ValidateUploadEnvelope(file);

        await using var input = file.OpenReadStream();
        using var image = await LoadImageAsync(input, cancellationToken);
        ValidateDimensions(image.Width, image.Height);

        image.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(AvatarOptions.OutputDimension, AvatarOptions.OutputDimension),
            Mode = ResizeMode.Crop,
            Position = AnchorPositionMode.Center
        }));

        Directory.CreateDirectory(StorageDirectory);

        var fileName = $"{userId:D}.webp";
        var outputPath = Path.Combine(StorageDirectory, fileName);
        await using var output = File.Create(outputPath);
        await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = AvatarOptions.WebpQuality }, cancellationToken);

        return BuildPublicUrl(fileName);
    }

    private string StorageDirectory
    {
        get
        {
            var configured = _options.StorageDirectory;
            return string.IsNullOrWhiteSpace(configured)
                ? Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "web", "public", "avatars"))
                : Path.GetFullPath(configured);
        }
    }

    private static void ValidateUploadEnvelope(IFormFile file)
    {
        if (file.Length == 0)
        {
            throw new ArgumentException("Avatar image is required.", nameof(file));
        }

        if (file.Length > AvatarOptions.MaxUploadBytes)
        {
            throw new ArgumentException("Avatar image must be 256 KB or smaller.", nameof(file));
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedContentTypes.Contains(file.ContentType) || !AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Avatar image must be a JPG, PNG, or WebP file.", nameof(file));
        }
    }

    private static async Task<Image> LoadImageAsync(Stream input, CancellationToken cancellationToken)
    {
        try
        {
            return await Image.LoadAsync(input, cancellationToken);
        }
        catch (UnknownImageFormatException ex)
        {
            throw new ArgumentException("Avatar image could not be read as JPG, PNG, or WebP.", nameof(input), ex);
        }
        catch (InvalidImageContentException ex)
        {
            throw new ArgumentException("Avatar image content is invalid.", nameof(input), ex);
        }
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width < AvatarOptions.MinDimension || height < AvatarOptions.MinDimension)
        {
            throw new ArgumentException("Avatar image must be at least 48x48 pixels.");
        }

        if (width > AvatarOptions.MaxDimension || height > AvatarOptions.MaxDimension)
        {
            throw new ArgumentException("Avatar image must be 960x960 pixels or smaller.");
        }
    }

    private string BuildPublicUrl(string fileName)
    {
        var publicPath = _options.PublicPath.Trim('/');
        var relativePath = $"/{publicPath}/{Uri.EscapeDataString(fileName)}";
        var baseUrl = (_configuration["WebApp:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? relativePath : $"{baseUrl}{relativePath}";
    }
}
