using PoolPredict.Api.Domain.Pools;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedPoolMessage
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public Guid AuthorMemberId { get; set; }
    public PoolMessageKind Kind { get; set; }
    public int? AnnouncementSlot { get; set; }
    public string Title { get; set; } = "";
    public string BodyMarkdown { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EditedAt { get; set; }
}
