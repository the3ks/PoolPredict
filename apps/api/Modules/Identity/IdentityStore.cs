using System.Security.Cryptography;
using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Identity;
using PoolPredict.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PoolPredict.Api.Modules.Identity;

public sealed class IdentityStore
{
    private const string EmailVerificationPurpose = "EmailVerification";
    private const string PasswordResetPurpose = "PasswordReset";
    private readonly List<User> _users = [];
    private readonly List<UserExternalLogin> _externalLogins = [];
    private readonly IDbContextFactory<PoolPredictDbContext> _dbContextFactory;
    private readonly object _gate = new();

    public IdentityStore(IConfiguration configuration, IDbContextFactory<PoolPredictDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        LoadPersisted();
        SeedPlatformAdmin(configuration);
    }

    public User Register(RegisterRequest request)
    {
        var email = NormalizeEmail(request.Email);
        var displayName = NormalizeDisplayName(request.DisplayName, email);

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters.", nameof(request));
        }

        lock (_gate)
        {
            if (_users.Any(user => user.NormalizedEmail == email.ToUpperInvariant()))
            {
                throw new InvalidOperationException("An account already exists for this email.");
            }

            var user = new User(
                Ids.NewId(),
                email,
                displayName,
                UserRole.PoolMember,
                PasswordService.Hash(request.Password));

            _users.Add(user);
            PersistUser(user);
            return user;
        }
    }

    public User Login(LoginRequest request)
    {
        var email = NormalizeEmail(request.Email);

        lock (_gate)
        {
            var user = _users.SingleOrDefault(candidate => candidate.NormalizedEmail == email.ToUpperInvariant());
            if (user?.PasswordHash is null || !PasswordService.Verify(request.Password, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Email or password is incorrect.");
            }

            if (!user.IsEmailVerified)
            {
                throw new InvalidOperationException("Verify your email address before logging in.");
            }

            user.MarkLoggedIn(DateTimeOffset.UtcNow);
            UpdateUser(user);
            return user;
        }
    }

    public User LoginWithGoogle(GoogleLoginRequest request)
    {
        var email = NormalizeEmail(request.Email);
        var providerUserId = string.IsNullOrWhiteSpace(request.GoogleSubject)
            ? throw new ArgumentException("Google subject is required.", nameof(request))
            : request.GoogleSubject.Trim();

        lock (_gate)
        {
            var existingLogin = _externalLogins.SingleOrDefault(login =>
                login.Provider == "Google" && login.ProviderUserId == providerUserId);

            if (existingLogin is not null)
            {
                var existingUser = _users.Single(user => user.Id == existingLogin.UserId);
                if (!existingUser.IsEmailVerified)
                {
                    existingUser.MarkEmailVerified(DateTimeOffset.UtcNow);
                }
                existingUser.MarkLoggedIn(DateTimeOffset.UtcNow);
                UpdateUser(existingUser);
                return existingUser;
            }

            var user = _users.SingleOrDefault(candidate => candidate.NormalizedEmail == email.ToUpperInvariant())
                ?? CreateGoogleUser(email, NormalizeDisplayName(request.DisplayName, email));

            if (!user.IsEmailVerified)
            {
                user.MarkEmailVerified(DateTimeOffset.UtcNow);
                UpdateUser(user);
            }

            var externalLogin = new UserExternalLogin(Ids.NewId(), user.Id, "Google", providerUserId);
            _externalLogins.Add(externalLogin);
            PersistExternalLogin(externalLogin);
            return user;
        }
    }

    public User? GetUser(Guid userId)
    {
        lock (_gate)
        {
            return _users.SingleOrDefault(user => user.Id == userId);
        }
    }

    public IReadOnlyCollection<AdminUserResponse> GetUsers(string? search)
    {
        lock (_gate)
        {
            var normalizedSearch = search?.Trim();
            return _users
                .Where(user => string.IsNullOrWhiteSpace(normalizedSearch)
                    || user.Email.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || user.DisplayName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(user => user.CreatedAt)
                .Take(100)
                .Select(ToAdminResponse)
                .ToArray();
        }
    }

    public string AdminResetPassword(Guid userId)
    {
        var temporaryPassword = GenerateTemporaryPassword();
        lock (_gate)
        {
            var user = _users.SingleOrDefault(candidate => candidate.Id == userId)
                ?? throw new ArgumentException("User was not found.", nameof(userId));

            user.SetPasswordHash(PasswordService.Hash(temporaryPassword), mustChangePassword: true);
            UpdateUser(user);
        }

        return temporaryPassword;
    }

    public AdminUserResponse AdminMarkEmailVerified(Guid userId)
    {
        lock (_gate)
        {
            var user = _users.SingleOrDefault(candidate => candidate.Id == userId)
                ?? throw new ArgumentException("User was not found.", nameof(userId));

            if (!user.IsEmailVerified)
            {
                user.MarkEmailVerified(DateTimeOffset.UtcNow);
                UpdateUser(user);
            }

            return ToAdminResponse(user);
        }
    }

    public void ChangePassword(Guid userId, ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            throw new ArgumentException("New password must be at least 8 characters.", nameof(request));
        }

        lock (_gate)
        {
            var user = _users.SingleOrDefault(candidate => candidate.Id == userId)
                ?? throw new UnauthorizedAccessException("User was not found.");

            if (user.PasswordHash is null || !PasswordService.Verify(request.CurrentPassword, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Current password is incorrect.");
            }

            user.SetPasswordHash(PasswordService.Hash(request.NewPassword));
            user.ClearMustChangePassword();
            UpdateUser(user);
        }
    }

    public string CreateEmailVerificationToken(Guid userId)
    {
        return CreateToken(userId, EmailVerificationPurpose, DateTimeOffset.UtcNow.AddHours(24));
    }

    public bool VerifyEmailToken(string rawToken)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var tokenHash = HashToken(rawToken);
        var token = db.IdentityTokens.SingleOrDefault(candidate =>
            candidate.Purpose == EmailVerificationPurpose &&
            candidate.TokenHash == tokenHash &&
            candidate.ConsumedAt == null);

        if (token is null || token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        lock (_gate)
        {
            var user = _users.SingleOrDefault(candidate => candidate.Id == token.UserId);
            if (user is null)
            {
                return false;
            }

            user.MarkEmailVerified(DateTimeOffset.UtcNow);
            UpdateUser(user);
            token.ConsumedAt = DateTimeOffset.UtcNow;
            db.SaveChanges();
            return true;
        }
    }

    public User? FindByEmail(string email)
    {
        var normalizedEmail = NormalizeEmail(email).ToUpperInvariant();
        lock (_gate)
        {
            return _users.SingleOrDefault(candidate => candidate.NormalizedEmail == normalizedEmail);
        }
    }

    public string CreatePasswordResetToken(Guid userId)
    {
        return CreateToken(userId, PasswordResetPurpose, DateTimeOffset.UtcNow.AddMinutes(45));
    }

    public bool ResetPassword(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            throw new ArgumentException("New password must be at least 8 characters.", nameof(request));
        }

        using var db = _dbContextFactory.CreateDbContext();
        var tokenHash = HashToken(request.Token);
        var token = db.IdentityTokens.SingleOrDefault(candidate =>
            candidate.Purpose == PasswordResetPurpose &&
            candidate.TokenHash == tokenHash &&
            candidate.ConsumedAt == null);

        if (token is null || token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        lock (_gate)
        {
            var user = _users.SingleOrDefault(candidate => candidate.Id == token.UserId);
            if (user is null)
            {
                return false;
            }

            user.SetPasswordHash(PasswordService.Hash(request.NewPassword));
            user.ClearMustChangePassword();
            UpdateUser(user);
            token.ConsumedAt = DateTimeOffset.UtcNow;
            db.SaveChanges();
            return true;
        }
    }

    private string CreateToken(Guid userId, string purpose, DateTimeOffset expiresAt)
    {
        var rawToken = GenerateToken();
        using var db = _dbContextFactory.CreateDbContext();
        db.IdentityTokens.Add(new PersistedIdentityToken
        {
            Id = Ids.NewId(),
            UserId = userId,
            Purpose = purpose,
            TokenHash = HashToken(rawToken),
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();
        return rawToken;
    }

    private User CreateGoogleUser(string email, string displayName)
    {
        var user = new User(Ids.NewId(), email, displayName, UserRole.PoolMember, emailVerifiedAt: DateTimeOffset.UtcNow);
        _users.Add(user);
        PersistUser(user);
        return user;
    }

    private void SeedPlatformAdmin(IConfiguration configuration)
    {
        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];
        var displayName = configuration["SeedAdmin:DisplayName"] ?? "Platform Admin";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var normalizedEmail = NormalizeEmail(email);
        var existingAdmin = _users.SingleOrDefault(user => user.NormalizedEmail == normalizedEmail.ToUpperInvariant());
        if (existingAdmin is not null)
        {
            if (!existingAdmin.IsEmailVerified)
            {
                existingAdmin.MarkEmailVerified(DateTimeOffset.UtcNow);
                UpdateUser(existingAdmin);
            }

            return;
        }

        var admin = new User(
            Ids.NewId(),
            normalizedEmail,
            NormalizeDisplayName(displayName, email),
            UserRole.PlatformAdmin,
            PasswordService.Hash(password),
            emailVerifiedAt: DateTimeOffset.UtcNow);

        _users.Add(admin);
        PersistUser(admin);
    }

    private void LoadPersisted()
    {
        using var db = _dbContextFactory.CreateDbContext();

        _users.AddRange(db.Users.AsNoTracking().Select(user => new User(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.PasswordHash,
            user.CreatedAt,
            user.EmailVerifiedAt,
            user.MustChangePassword,
            user.LastLoginAt,
            user.UpdatedAt)));

        _externalLogins.AddRange(db.UserExternalLogins.AsNoTracking().Select(login => new UserExternalLogin(
            login.Id,
            login.UserId,
            login.Provider,
            login.ProviderUserId)));
    }

    private void PersistUser(User user)
    {
        using var db = _dbContextFactory.CreateDbContext();
        if (db.Users.Any(candidate => candidate.Id == user.Id || candidate.NormalizedEmail == user.NormalizedEmail))
        {
            return;
        }

        db.Users.Add(ToPersistedUser(user));
        db.SaveChanges();
    }

    private void UpdateUser(User user)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var persisted = db.Users.SingleOrDefault(candidate => candidate.Id == user.Id);
        if (persisted is null)
        {
            return;
        }

        persisted.DisplayName = user.DisplayName;
        persisted.Role = user.Role;
        persisted.PasswordHash = user.PasswordHash;
        persisted.EmailVerifiedAt = user.EmailVerifiedAt;
        persisted.MustChangePassword = user.MustChangePassword;
        persisted.LastLoginAt = user.LastLoginAt;
        persisted.UpdatedAt = user.UpdatedAt;
        db.SaveChanges();
    }

    private static PersistedUser ToPersistedUser(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        NormalizedEmail = user.NormalizedEmail,
        DisplayName = user.DisplayName,
        Role = user.Role,
        PasswordHash = user.PasswordHash,
        CreatedAt = user.CreatedAt,
        EmailVerifiedAt = user.EmailVerifiedAt,
        MustChangePassword = user.MustChangePassword,
        LastLoginAt = user.LastLoginAt,
        UpdatedAt = user.UpdatedAt
    };

    private void PersistExternalLogin(UserExternalLogin login)
    {
        using var db = _dbContextFactory.CreateDbContext();
        if (db.UserExternalLogins.Any(candidate => candidate.Provider == login.Provider && candidate.ProviderUserId == login.ProviderUserId))
        {
            return;
        }

        db.UserExternalLogins.Add(new PersistedUserExternalLogin
        {
            Id = login.Id,
            UserId = login.UserId,
            Provider = login.Provider,
            ProviderUserId = login.ProviderUserId
        });
        db.SaveChanges();
    }

    private static AdminUserResponse ToAdminResponse(User user) =>
        new(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.IsEmailVerified,
            user.MustChangePassword,
            user.CreatedAt,
            user.LastLoginAt);

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("A valid email is required.", nameof(email));
        }

        return email.Trim();
    }

    private static string NormalizeDisplayName(string displayName, string email)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? email.Split('@')[0]
            : displayName.Trim();
    }

    private static string GenerateToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string GenerateTemporaryPassword() => $"Pp-{Base64UrlEncode(RandomNumberGenerator.GetBytes(12))}1!";

    private static string HashToken(string rawToken)
    {
        var tokenBytes = System.Text.Encoding.UTF8.GetBytes(rawToken.Trim());
        return Base64UrlEncode(SHA256.HashData(tokenBytes));
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    bool IsEmailVerified,
    bool MustChangePassword,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);
