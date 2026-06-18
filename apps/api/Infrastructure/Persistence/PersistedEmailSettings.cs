namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedEmailSettings
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "AwsSesSmtp";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string? Password { get; set; }
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "PoolPredict";
    public bool UseStartTls { get; set; } = true;
    public bool IsEnabled { get; set; }
    public string? BackupToEmail { get; set; }
    public DateTimeOffset? BackupUpdatedAt { get; set; }
    public DateTimeOffset? BackupLastSentAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
