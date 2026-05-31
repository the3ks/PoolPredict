using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Identity;

public sealed class User : Entity
{
    public User(Guid id, string email, string displayName, UserRole role, string? passwordHash = null)
        : base(id)
    {
        Email = email;
        NormalizedEmail = email.Trim().ToUpperInvariant();
        DisplayName = displayName;
        Role = role;
        PasswordHash = passwordHash;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string Email { get; }

    public string NormalizedEmail { get; }

    public string DisplayName { get; private set; }

    public UserRole Role { get; private set; }

    public string? PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;
}
