using PoolPredict.Api.Domain.Identity;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; }
    public string? PasswordHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
