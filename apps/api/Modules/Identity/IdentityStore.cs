using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Identity;
using PoolPredict.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PoolPredict.Api.Modules.Identity;

public sealed class IdentityStore
{
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
                return _users.Single(user => user.Id == existingLogin.UserId);
            }

            var user = _users.SingleOrDefault(candidate => candidate.NormalizedEmail == email.ToUpperInvariant())
                ?? CreateGoogleUser(email, NormalizeDisplayName(request.DisplayName, email));

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

    private User CreateGoogleUser(string email, string displayName)
    {
        var user = new User(Ids.NewId(), email, displayName, UserRole.PoolMember);
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
        if (_users.Any(user => user.NormalizedEmail == normalizedEmail.ToUpperInvariant()))
        {
            return;
        }

        var admin = new User(
            Ids.NewId(),
            normalizedEmail,
            NormalizeDisplayName(displayName, email),
            UserRole.PlatformAdmin,
            PasswordService.Hash(password));

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
            user.PasswordHash)));

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

        db.Users.Add(new PersistedUser
        {
            Id = user.Id,
            Email = user.Email,
            NormalizedEmail = user.NormalizedEmail,
            DisplayName = user.DisplayName,
            Role = user.Role,
            PasswordHash = user.PasswordHash,
            CreatedAt = user.CreatedAt
        });
        db.SaveChanges();
    }

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
}
