namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedIdentityToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Purpose { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
