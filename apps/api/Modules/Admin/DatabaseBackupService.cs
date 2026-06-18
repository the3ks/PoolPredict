using System.Diagnostics;
using System.IO.Compression;
using MySqlConnector;
using PoolPredict.Api.Modules.Email;

namespace PoolPredict.Api.Modules.Admin;

public sealed class DatabaseBackupService(
    IConfiguration configuration,
    SmtpEmailSender emailSender,
    DatabaseBackupSettingsStore settingsStore,
    ILogger<DatabaseBackupService> logger)
{
    private static readonly string[] WindowsDumpExecutables = ["mariadb-dump.exe", "mysqldump.exe"];
    private static readonly string[] UnixDumpExecutables = ["mariadb-dump", "mysqldump"];

    public async Task<DatabaseBackupSendResponse> CreateAndSendAsync(string recipientEmail, CancellationToken cancellationToken = default)
    {
        var normalizedRecipientEmail = NormalizeRequiredEmail(recipientEmail);
        var connectionString = configuration.GetConnectionString("MariaDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:MariaDb is required to generate a database backup.");
        }

        var connectionBuilder = new MySqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(connectionBuilder.Database))
        {
            throw new InvalidOperationException("ConnectionStrings:MariaDb must include a database name.");
        }

        var dumpExecutable = ResolveDumpExecutable();
        var dateStamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmm");
        var sqlFileName = $"poolpredict_db_{dateStamp}.sql";
        var zipFileName = $"poolpredict_db_{dateStamp}.zip";
        var tempDirectory = Path.Combine(Path.GetTempPath(), "PoolPredict", "db-backup", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempDirectory);
        var archiveFilePath = ResolveArchiveFilePath(zipFileName);

        var sqlFilePath = Path.Combine(tempDirectory, sqlFileName);
        var zipFilePath = Path.Combine(tempDirectory, zipFileName);

        try
        {
            await DumpDatabaseAsync(dumpExecutable, connectionBuilder, sqlFilePath, cancellationToken);
            await CreateZipAsync(sqlFilePath, sqlFileName, zipFilePath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(archiveFilePath))
            {
                await PersistArchiveCopyAsync(zipFilePath, archiveFilePath, cancellationToken);
            }

            var attachment = new EmailAttachment(
                zipFileName,
                "application/zip",
                await File.ReadAllBytesAsync(zipFilePath, cancellationToken));

            var sendResult = await emailSender.SendAsync(
                normalizedRecipientEmail,
                $"PoolPredict database backup {dateStamp}",
                $"Attached is the PoolPredict database backup generated on {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.",
                attachments: [attachment]);

            if (!sendResult.Sent)
            {
                throw new InvalidOperationException(sendResult.Message);
            }

            settingsStore.MarkSent(normalizedRecipientEmail);
            return new DatabaseBackupSendResponse(
                string.IsNullOrWhiteSpace(archiveFilePath)
                    ? "Database backup generated, zipped, and emailed successfully."
                    : $"Database backup generated, zipped, saved to {archiveFilePath}, and emailed successfully.",
                zipFileName,
                normalizedRecipientEmail);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch (Exception cleanupError)
            {
                logger.LogWarning(cleanupError, "Could not clean temporary database backup directory {TempDirectory}", tempDirectory);
            }
        }
    }

    private static async Task CreateZipAsync(string sqlFilePath, string sqlFileName, string zipFilePath, CancellationToken cancellationToken)
    {
        await using var zipStream = File.Create(zipFilePath);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false);
        var entry = archive.CreateEntry(sqlFileName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await using var sqlStream = File.OpenRead(sqlFilePath);
        await sqlStream.CopyToAsync(entryStream, cancellationToken);
    }

    private static async Task PersistArchiveCopyAsync(string sourceZipFilePath, string destinationZipFilePath, CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationZipFilePath);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new InvalidOperationException("DatabaseBackup:ArchiveDirectory resolved to an invalid path.");
        }

        Directory.CreateDirectory(destinationDirectory);
        await using var sourceStream = File.OpenRead(sourceZipFilePath);
        await using var destinationStream = File.Create(destinationZipFilePath);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
    }

    private static async Task DumpDatabaseAsync(
        string dumpExecutable,
        MySqlConnectionStringBuilder connectionBuilder,
        string sqlFilePath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = dumpExecutable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(connectionBuilder.Server))
        {
            startInfo.ArgumentList.Add($"--host={connectionBuilder.Server}");
        }

        if (connectionBuilder.Port > 0)
        {
            startInfo.ArgumentList.Add($"--port={connectionBuilder.Port}");
        }

        if (!string.IsNullOrWhiteSpace(connectionBuilder.UserID))
        {
            startInfo.ArgumentList.Add($"--user={connectionBuilder.UserID}");
        }

        if (!string.IsNullOrWhiteSpace(connectionBuilder.Password))
        {
            startInfo.ArgumentList.Add($"--password={connectionBuilder.Password}");
        }

        startInfo.ArgumentList.Add("--single-transaction");
        startInfo.ArgumentList.Add("--routines");
        startInfo.ArgumentList.Add("--events");
        startInfo.ArgumentList.Add("--triggers");
        startInfo.ArgumentList.Add("--databases");
        startInfo.ArgumentList.Add(connectionBuilder.Database);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start database dump executable '{dumpExecutable}'.");

        await using var sqlStream = File.Create(sqlFilePath);
        var copyOutputTask = process.StandardOutput.BaseStream.CopyToAsync(sqlStream, cancellationToken);
        var readErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await Task.WhenAll(copyOutputTask, readErrorTask, process.WaitForExitAsync(cancellationToken));

        if (process.ExitCode != 0)
        {
            var stderr = readErrorTask.Result.Trim();
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(stderr)
                    ? $"Database dump failed with exit code {process.ExitCode}."
                    : $"Database dump failed: {stderr}");
        }

        var fileInfo = new FileInfo(sqlFilePath);
        if (!fileInfo.Exists || fileInfo.Length == 0)
        {
            throw new InvalidOperationException("Database dump did not produce any SQL output.");
        }
    }

    private string ResolveDumpExecutable()
    {
        var configuredPath = configuration["DatabaseBackup:DumpExecutablePath"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (!File.Exists(configuredPath))
            {
                throw new InvalidOperationException($"Configured dump executable was not found: {configuredPath}");
            }

            return configuredPath;
        }

        foreach (var candidate in OperatingSystem.IsWindows() ? WindowsDumpExecutables : UnixDumpExecutables)
        {
            if (TryResolveFromPath(candidate, out var resolvedPath))
            {
                return resolvedPath;
            }
        }

        throw new InvalidOperationException(
            "MariaDB dump executable was not found. Install mariadb-dump/mysqldump or configure DatabaseBackup:DumpExecutablePath.");
    }

    private string? ResolveArchiveFilePath(string zipFileName)
    {
        var archiveDirectory = configuration["DatabaseBackup:ArchiveDirectory"]?.Trim();
        if (string.IsNullOrWhiteSpace(archiveDirectory))
        {
            return null;
        }

        var fullArchiveDirectory = Path.GetFullPath(archiveDirectory);
        return Path.Combine(fullArchiveDirectory, zipFileName);
    }

    private static bool TryResolveFromPath(string fileName, out string resolvedPath)
    {
        if (File.Exists(fileName))
        {
            resolvedPath = Path.GetFullPath(fileName);
            return true;
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (var segment in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var candidate = Path.Combine(segment, fileName);
                if (File.Exists(candidate))
                {
                    resolvedPath = candidate;
                    return true;
                }
            }
        }

        resolvedPath = "";
        return false;
    }

    private static string NormalizeRequiredEmail(string email)
    {
        try
        {
            return new System.Net.Mail.MailAddress(email.Trim()).Address;
        }
        catch (Exception)
        {
            throw new ArgumentException("A valid backup recipient email is required.", nameof(email));
        }
    }
}
