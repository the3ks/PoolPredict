namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedPoolJoinRequest
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public string Status { get; set; } = "Pending";
}
