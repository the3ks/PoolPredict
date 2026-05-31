using PoolPredict.Api.Domain.Pools;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedPoolMember
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public Guid UserId { get; set; }
    public PoolMemberRole Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}
