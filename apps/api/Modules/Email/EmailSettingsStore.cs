using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Infrastructure.Persistence;

namespace PoolPredict.Api.Modules.Email;

public sealed class EmailSettingsStore(IDbContextFactory<PoolPredictDbContext> dbContextFactory)
{
    public EmailSettingsResponse Get()
    {
        using var db = dbContextFactory.CreateDbContext();
        var settings = db.EmailSettings.AsNoTracking().SingleOrDefault();
        return settings is null ? EmailSettingsResponse.Default : ToResponse(settings);
    }

    public EmailSettingsResponse Save(UpdateEmailSettingsRequest request)
    {
        if (request.IsEnabled && (string.IsNullOrWhiteSpace(request.Host) || string.IsNullOrWhiteSpace(request.FromEmail)))
        {
            throw new ArgumentException("Host and from email are required when email is enabled.");
        }

        using var db = dbContextFactory.CreateDbContext();
        var settings = db.EmailSettings.SingleOrDefault();
        if (settings is null)
        {
            settings = new PersistedEmailSettings { Id = Guid.NewGuid() };
            db.EmailSettings.Add(settings);
        }

        settings.Provider = string.IsNullOrWhiteSpace(request.Provider) ? "AwsSesSmtp" : request.Provider.Trim();
        settings.Host = request.Host.Trim();
        settings.Port = request.Port <= 0 ? 587 : request.Port;
        settings.Username = request.Username.Trim();
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            settings.Password = request.Password;
        }
        settings.FromEmail = request.FromEmail.Trim();
        settings.FromName = string.IsNullOrWhiteSpace(request.FromName) ? "PoolPredict" : request.FromName.Trim();
        settings.UseStartTls = request.UseStartTls;
        settings.IsEnabled = request.IsEnabled;
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        db.SaveChanges();
        return ToResponse(settings);
    }

    public PersistedEmailSettings? GetEnabledSettings()
    {
        using var db = dbContextFactory.CreateDbContext();
        return db.EmailSettings.AsNoTracking().SingleOrDefault(settings => settings.IsEnabled);
    }

    private static EmailSettingsResponse ToResponse(PersistedEmailSettings settings) =>
        new(
            settings.Provider,
            settings.Host,
            settings.Port,
            settings.Username,
            HasPassword: !string.IsNullOrWhiteSpace(settings.Password),
            settings.FromEmail,
            settings.FromName,
            settings.UseStartTls,
            settings.IsEnabled,
            settings.UpdatedAt);
}

public sealed record EmailSettingsResponse(
    string Provider,
    string Host,
    int Port,
    string Username,
    bool HasPassword,
    string FromEmail,
    string FromName,
    bool UseStartTls,
    bool IsEnabled,
    DateTimeOffset? UpdatedAt)
{
    public static EmailSettingsResponse Default { get; } = new(
        "AwsSesSmtp",
        "",
        587,
        "",
        false,
        "",
        "PoolPredict",
        true,
        false,
        null);
}

public sealed record UpdateEmailSettingsRequest(
    string Provider,
    string Host,
    int Port,
    string Username,
    string? Password,
    string FromEmail,
    string FromName,
    bool UseStartTls,
    bool IsEnabled);

public sealed record TestEmailRequest(string ToEmail);
