using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Identity;

public sealed class UserExternalLogin : Entity
{
    public UserExternalLogin(Guid id, Guid userId, string provider, string providerUserId)
        : base(id)
    {
        UserId = userId;
        Provider = provider;
        ProviderUserId = providerUserId;
        LinkedAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; }

    public string Provider { get; }

    public string ProviderUserId { get; }

    public DateTimeOffset LinkedAt { get; }
}
