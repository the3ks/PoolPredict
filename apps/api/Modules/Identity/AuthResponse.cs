using PoolPredict.Api.Domain.Identity;

namespace PoolPredict.Api.Modules.Identity;

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    UserProfileResponse User);

public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    UserRole Role,
    bool IsEmailVerified,
    bool MustChangePassword);
