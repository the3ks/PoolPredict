using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Identity;

public sealed class User : Entity
{
    public User(
        Guid id,
        string email,
        string displayName,
        UserRole role,
        string? passwordHash = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? emailVerifiedAt = null,
        bool mustChangePassword = false,
        DateTimeOffset? lastLoginAt = null,
        DateTimeOffset? updatedAt = null)
        : base(id)
    {
        Email = email;
        NormalizedEmail = email.Trim().ToUpperInvariant();
        DisplayName = displayName;
        Role = role;
        PasswordHash = passwordHash;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        EmailVerifiedAt = emailVerifiedAt;
        MustChangePassword = mustChangePassword;
        LastLoginAt = lastLoginAt;
        UpdatedAt = updatedAt;
    }

    public string Email { get; }

    public string NormalizedEmail { get; }

    public string DisplayName { get; private set; }

    public UserRole Role { get; private set; }

    public string? PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? EmailVerifiedAt { get; private set; }

    public bool MustChangePassword { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public bool IsEmailVerified => EmailVerifiedAt is not null;

    public void SetPasswordHash(string passwordHash, bool mustChangePassword = false)
    {
        PasswordHash = passwordHash;
        MustChangePassword = mustChangePassword;
        Touch();
    }

    public void MarkEmailVerified(DateTimeOffset verifiedAt)
    {
        EmailVerifiedAt = verifiedAt;
        Touch();
    }

    public void MarkLoggedIn(DateTimeOffset loggedInAt)
    {
        LastLoginAt = loggedInAt;
        Touch();
    }

    public void ClearMustChangePassword()
    {
        MustChangePassword = false;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
