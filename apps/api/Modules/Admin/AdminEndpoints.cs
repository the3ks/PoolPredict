using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Modules.Email;
using PoolPredict.Api.Modules.Identity;
using PoolPredict.Api.Modules.Pools;
using PoolPredict.Api.Infrastructure.Persistence;

namespace PoolPredict.Api.Modules.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization();

        group.MapGet("/users", (string? search, ClaimsPrincipal principal, IdentityStore users) =>
        {
            if (!IsPlatformAdmin(principal))
            {
                return Results.Forbid();
            }

            return Results.Ok(users.GetUsers(search));
        });

        group.MapGet("/pools", async (
            ClaimsPrincipal principal,
            IDbContextFactory<PoolPredictDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            if (!IsPlatformAdmin(principal))
            {
                return Results.Forbid();
            }

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var rows = await (
                from pool in db.Pools.AsNoTracking()
                join owner in db.Users.AsNoTracking()
                    on pool.OwnerUserId equals owner.Id
                join tournament in db.Tournaments.AsNoTracking()
                    on pool.TournamentId equals tournament.Id
                select new
                {
                    pool.Id,
                    pool.Name,
                    pool.OwnerUserId,
                    OwnerDisplayName = owner.DisplayName,
                    OwnerEmail = owner.Email,
                    pool.TournamentId,
                    TournamentName = tournament.Name,
                    tournament.Provider,
                    tournament.IsTestData,
                    pool.Profile,
                    pool.StartingBalance,
                    pool.IsHidden,
                    MemberCount = db.PoolMembers.Count(member => member.PoolId == pool.Id),
                    InviteCount = db.PoolInvites.Count(invite => invite.PoolId == pool.Id)
                })
                .OrderBy(pool => pool.Name)
                .Take(200)
                .ToArrayAsync(cancellationToken);

            var pools = rows
                .Select(pool => new AdminPoolResponse(
                    pool.Id,
                    pool.Name,
                    pool.OwnerUserId,
                    pool.OwnerDisplayName,
                    pool.OwnerEmail,
                    pool.TournamentId,
                    pool.TournamentName,
                    pool.Provider,
                    pool.IsTestData,
                    pool.Profile.ToString(),
                    pool.StartingBalance,
                    pool.IsHidden,
                    pool.MemberCount,
                    pool.InviteCount))
                .ToArray();

            return Results.Ok(pools);
        });

        group.MapPut("/pools/{poolId:guid}/visibility", (Guid poolId, UpdatePoolVisibilityRequest request, ClaimsPrincipal principal, PoolStore pools) =>
        {
            if (!IsPlatformAdmin(principal))
            {
                return Results.Forbid();
            }

            try
            {
                var pool = pools.SetPoolHidden(poolId, request.IsHidden);
                return Results.Ok(new PoolVisibilityResponse(pool.Id, pool.IsHidden));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapPost("/users/{userId:guid}/password-reset", (Guid userId, ClaimsPrincipal principal, IdentityStore users) =>
        {
            if (!IsPlatformAdmin(principal))
            {
                return Results.Forbid();
            }

            try
            {
                var temporaryPassword = users.AdminResetPassword(userId);
                return Results.Ok(new AdminPasswordResetResponse(
                    "Password reset. Share this temporary password with the user; they should change it after logging in.",
                    temporaryPassword));
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        group.MapPost("/users/{userId:guid}/verify-email", (Guid userId, ClaimsPrincipal principal, IdentityStore users) =>
        {
            if (!IsPlatformAdmin(principal))
            {
                return Results.Forbid();
            }

            try
            {
                return Results.Ok(users.AdminMarkEmailVerified(userId));
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        group.MapGet("/email-settings", (ClaimsPrincipal principal, EmailSettingsStore settings) =>
        {
            if (!IsPlatformAdmin(principal))
            {
                return Results.Forbid();
            }

            return Results.Ok(settings.Get());
        });

        group.MapPut("/email-settings", (UpdateEmailSettingsRequest request, ClaimsPrincipal principal, EmailSettingsStore settings) =>
        {
            if (!IsPlatformAdmin(principal))
            {
                return Results.Forbid();
            }

            try
            {
                return Results.Ok(settings.Save(request));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/email-settings/test", async (TestEmailRequest request, ClaimsPrincipal principal, SmtpEmailSender emailSender) =>
        {
            if (!IsPlatformAdmin(principal))
            {
                return Results.Forbid();
            }

            var result = await emailSender.SendAsync(
                request.ToEmail,
                "PoolPredict SMTP test",
                "PoolPredict SMTP settings are working.");

            return result.Sent
                ? Results.Ok(new { message = result.Message })
                : Results.BadRequest(new { error = result.Message });
        });

        group.MapGet("/database-backup/settings", (ClaimsPrincipal principal, DatabaseBackupSettingsStore settings) =>
        {
            if (!IsPlatformAdmin(principal))
            {
                return Results.Forbid();
            }

            return Results.Ok(settings.Get());
        });

        group.MapPut("/database-backup/settings", (UpdateDatabaseBackupSettingsRequest request, ClaimsPrincipal principal, DatabaseBackupSettingsStore settings) =>
        {
            if (!IsPlatformAdmin(principal))
            {
                return Results.Forbid();
            }

            try
            {
                return Results.Ok(settings.Save(request));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/database-backup/send", async (SendDatabaseBackupRequest request, ClaimsPrincipal principal, DatabaseBackupService backups, CancellationToken cancellationToken) =>
        {
            if (!IsPlatformAdmin(principal))
            {
                return Results.Forbid();
            }

            try
            {
                return Results.Ok(await backups.CreateAndSendAsync(request.RecipientEmail, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }

    private static bool IsPlatformAdmin(ClaimsPrincipal principal) => principal.IsInRole("PlatformAdmin");
}

public sealed record AdminPasswordResetResponse(string Message, string TemporaryPassword);

public sealed record AdminPoolResponse(
    Guid Id,
    string Name,
    Guid OwnerUserId,
    string OwnerDisplayName,
    string OwnerEmail,
    Guid TournamentId,
    string TournamentName,
    string Provider,
    bool IsTestData,
    string Profile,
    int StartingBalance,
    bool IsHidden,
    int MemberCount,
    int InviteCount);

public sealed record UpdatePoolVisibilityRequest(bool IsHidden);

public sealed record PoolVisibilityResponse(Guid Id, bool IsHidden);
