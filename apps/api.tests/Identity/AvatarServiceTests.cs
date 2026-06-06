using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using PoolPredict.Api.Modules.Identity;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PoolPredict.Api.Tests.Identity;

public sealed class AvatarServiceTests
{
    [Fact]
    public async Task SavesUploadedAvatarAsSmall48PixelWebp()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var service = CreateService(tempDirectory);
            var userId = Guid.NewGuid();
            await using var upload = CreatePngUpload(width: 96, height: 80);
            var formFile = CreateFormFile(upload, "avatar.png", "image/png");

            var avatarUrl = await service.SaveAvatarAsync(userId, formFile, CancellationToken.None);
            var fileName = Path.GetFileName(new Uri(avatarUrl).LocalPath);
            var storedPath = Path.Combine(tempDirectory, fileName);

            Assert.Equal($"{userId:D}.webp", fileName);
            Assert.EndsWith(".webp", avatarUrl);
            Assert.True(File.Exists(storedPath));
            Assert.True(new FileInfo(storedPath).Length < 10 * 1024);

            using var storedImage = await Image.LoadAsync(storedPath);
            Assert.Equal(48, storedImage.Width);
            Assert.Equal(48, storedImage.Height);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task UsesLocalhostWebBaseUrlWhenConfigurationIsMissing()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AvatarStorage:StorageDirectory"] = tempDirectory
                })
                .Build();
            var service = new AvatarService(new TestWebHostEnvironment(), configuration);
            await using var upload = CreatePngUpload(width: 96, height: 96);

            var avatarUrl = await service.SaveAvatarAsync(Guid.NewGuid(), CreateFormFile(upload, "avatar.png", "image/png"), CancellationToken.None);

            Assert.StartsWith("http://localhost:3000/avatars/", avatarUrl, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RejectsUploadLargerThan256Kb()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var service = CreateService(tempDirectory);
            await using var upload = new MemoryStream(new byte[(256 * 1024) + 1]);
            var formFile = CreateFormFile(upload, "avatar.png", "image/png");

            var error = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SaveAvatarAsync(Guid.NewGuid(), formFile, CancellationToken.None));

            Assert.Contains("256 KB", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task UploadingAgainReplacesCurrentUserAvatarFile()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var service = CreateService(tempDirectory);
            var userId = Guid.NewGuid();
            await using var firstUpload = CreatePngUpload(width: 96, height: 96);
            await using var secondUpload = CreatePngUpload(width: 80, height: 96);

            var firstUrl = await service.SaveAvatarAsync(userId, CreateFormFile(firstUpload, "avatar.png", "image/png"), CancellationToken.None);
            var secondUrl = await service.SaveAvatarAsync(userId, CreateFormFile(secondUpload, "avatar.png", "image/png"), CancellationToken.None);

            Assert.Equal(firstUrl, secondUrl);
            Assert.Single(Directory.EnumerateFiles(tempDirectory, "*.webp", SearchOption.TopDirectoryOnly));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static AvatarService CreateService(string storageDirectory)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AvatarStorage:StorageDirectory"] = storageDirectory,
                ["WebApp:BaseUrl"] = "https://poolpredict.test"
            })
            .Build();

        return new AvatarService(new TestWebHostEnvironment(), configuration);
    }

    private static MemoryStream CreatePngUpload(int width, int height)
    {
        var stream = new MemoryStream();
        using (var image = new Image<Rgba32>(width, height, Color.CornflowerBlue))
        {
            image.SaveAsPng(stream);
        }

        stream.Position = 0;
        return stream;
    }

    private static FormFile CreateFormFile(Stream stream, string fileName, string contentType) => new(
        stream,
        0,
        stream.Length,
        "avatar",
        fileName)
    {
        Headers = new HeaderDictionary(),
        ContentType = contentType
    };

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "poolpredict-avatar-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PoolPredict.Api.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
