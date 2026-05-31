namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedUserExternalLogin
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = "";
    public string ProviderUserId { get; set; } = "";
}
