using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Infrastructure.Persistence;

namespace PoolPredict.Api.Modules.Admin;

public sealed class DatabaseBackupSettingsStore(IDbContextFactory<PoolPredictDbContext> dbContextFactory)
{
    public DatabaseBackupSettingsResponse Get()
    {
        using var db = dbContextFactory.CreateDbContext();
        var settings = db.EmailSettings.AsNoTracking().SingleOrDefault();
        return settings is null
            ? DatabaseBackupSettingsResponse.Default
            : new DatabaseBackupSettingsResponse(
                settings.BackupToEmail ?? "",
                settings.BackupUpdatedAt,
                settings.BackupLastSentAt);
    }

    public DatabaseBackupSettingsResponse Save(UpdateDatabaseBackupSettingsRequest request)
    {
        var recipientEmail = NormalizeRequiredEmail(request.RecipientEmail);

        using var db = dbContextFactory.CreateDbContext();
        var settings = db.EmailSettings.SingleOrDefault();
        if (settings is null)
        {
            settings = new PersistedEmailSettings { Id = Guid.NewGuid() };
            db.EmailSettings.Add(settings);
        }

        settings.BackupToEmail = recipientEmail;
        settings.BackupUpdatedAt = DateTimeOffset.UtcNow;
        db.SaveChanges();

        return new DatabaseBackupSettingsResponse(
            settings.BackupToEmail,
            settings.BackupUpdatedAt,
            settings.BackupLastSentAt);
    }

    public void MarkSent(string recipientEmail)
    {
        var normalizedRecipientEmail = NormalizeRequiredEmail(recipientEmail);

        using var db = dbContextFactory.CreateDbContext();
        var settings = db.EmailSettings.SingleOrDefault();
        if (settings is null)
        {
            settings = new PersistedEmailSettings
            {
                Id = Guid.NewGuid(),
                BackupToEmail = normalizedRecipientEmail,
                BackupUpdatedAt = DateTimeOffset.UtcNow,
                BackupLastSentAt = DateTimeOffset.UtcNow
            };
            db.EmailSettings.Add(settings);
        }
        else
        {
            settings.BackupToEmail = normalizedRecipientEmail;
            settings.BackupUpdatedAt = DateTimeOffset.UtcNow;
            settings.BackupLastSentAt = DateTimeOffset.UtcNow;
        }

        db.SaveChanges();
    }

    public string? GetRecipientEmail()
    {
        using var db = dbContextFactory.CreateDbContext();
        return db.EmailSettings
            .AsNoTracking()
            .Select(settings => settings.BackupToEmail)
            .SingleOrDefault();
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

public sealed record DatabaseBackupSettingsResponse(
    string RecipientEmail,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? LastSentAt)
{
    public static DatabaseBackupSettingsResponse Default { get; } = new(
        "",
        null,
        null);
}

public sealed record UpdateDatabaseBackupSettingsRequest(string RecipientEmail);

public sealed record SendDatabaseBackupRequest(string RecipientEmail);

public sealed record DatabaseBackupSendResponse(string Message, string FileName, string RecipientEmail);
