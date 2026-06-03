namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedPoolInvite
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Code { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }
}
