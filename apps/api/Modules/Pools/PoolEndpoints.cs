using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Pools;
using PoolPredict.Api.Infrastructure.Persistence;
using PoolPredict.Api.Modules.Predictions;
using PoolPredict.Api.Modules.Tournaments;
using System.Security.Claims;

namespace PoolPredict.Api.Modules.Pools;

public static class PoolEndpoints
{
    public static IEndpointRouteBuilder MapPoolEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pools");

        group.MapGet("/", (ClaimsPrincipal principal, PoolStore pools) =>
        {
            return TryGetUserId(principal, out var userId)
                ? Results.Ok(pools.GetPoolsForUser(userId))
                : Results.Unauthorized();
        }).RequireAuthorization();

        group.MapGet("/discover", async (
            ClaimsPrincipal principal,
            IDbContextFactory<PoolPredictDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var rows = await (
                from pool in db.Pools.AsNoTracking()
                join owner in db.Users.AsNoTracking()
                    on pool.OwnerUserId equals owner.Id
                join tournament in db.Tournaments.AsNoTracking()
                    on pool.TournamentId equals tournament.Id
                where !db.PoolMembers.Any(member => member.PoolId == pool.Id && member.UserId == userId)
                select new
                {
                    pool.Id,
                    pool.Name,
                    OwnerDisplayName = owner.DisplayName,
                    OwnerEmail = owner.Email,
                    TournamentName = tournament.Name,
                    tournament.Provider,
                    tournament.IsTestData,
                    pool.Profile,
                    pool.StartingBalance,
                    MemberCount = db.PoolMembers.Count(member => member.PoolId == pool.Id),
                    InviteCount = db.PoolInvites.Count(invite => invite.PoolId == pool.Id && invite.RevokedAt == null),
                    HasPendingJoinRequest = db.PoolJoinRequests.Any(request =>
                        request.PoolId == pool.Id &&
                        request.UserId == userId &&
                        request.Status == "Pending")
                })
                .OrderBy(pool => pool.Name)
                .Take(100)
                .ToArrayAsync(cancellationToken);

            return Results.Ok(rows.Select(pool => new DiscoverPoolResponse(
                pool.Id,
                pool.Name,
                pool.OwnerDisplayName,
                pool.OwnerEmail,
                pool.TournamentName,
                pool.Provider,
                pool.IsTestData,
                pool.Profile.ToString(),
                pool.StartingBalance,
                pool.MemberCount,
                pool.InviteCount,
                pool.HasPendingJoinRequest)));
        }).RequireAuthorization();

        group.MapGet("/latest", async (
            IDbContextFactory<PoolPredictDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var rows = await (
                from pool in db.Pools.AsNoTracking()
                join owner in db.Users.AsNoTracking()
                    on pool.OwnerUserId equals owner.Id
                join tournament in db.Tournaments.AsNoTracking()
                    on pool.TournamentId equals tournament.Id
                join ownerMember in db.PoolMembers.AsNoTracking()
                    on new { PoolId = pool.Id, UserId = pool.OwnerUserId } equals new { ownerMember.PoolId, ownerMember.UserId }
                select new
                {
                    pool.Id,
                    pool.Name,
                    OwnerDisplayName = owner.DisplayName,
                    TournamentName = tournament.Name,
                    tournament.Provider,
                    tournament.IsTestData,
                    pool.Profile,
                    pool.StartingBalance,
                    ownerMember.JoinedAt,
                    MemberCount = db.PoolMembers.Count(member => member.PoolId == pool.Id),
                    InviteCount = db.PoolInvites.Count(invite => invite.PoolId == pool.Id && invite.RevokedAt == null)
                })
                .OrderByDescending(pool => pool.JoinedAt)
                .Take(3)
                .ToArrayAsync(cancellationToken);

            return Results.Ok(rows.Select(pool => new DiscoverPoolResponse(
                pool.Id,
                pool.Name,
                pool.OwnerDisplayName,
                "",
                pool.TournamentName,
                pool.Provider,
                pool.IsTestData,
                pool.Profile.ToString(),
                pool.StartingBalance,
                pool.MemberCount,
                pool.InviteCount,
                false)));
        });

        group.MapPost("/", (ClaimsPrincipal principal, CreatePoolRequest request, PoolStore pools, TournamentCatalog catalog, PredictionStore predictions) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                if (request.Profile == MarketProfile.Expert && !principal.IsInRole("PlatformAdmin"))
                {
                    return Results.Forbid();
                }

                var pool = pools.CreatePool(userId, request, catalog);
                var owner = pools.GetMember(pool.Id, userId);
                if (owner is not null)
                {
                    predictions.InitializeMemberBalance(pool.Id, owner.Id, pool.StartingBalance);
                }
                return Results.Created($"/api/pools/{pool.Id}", pool);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        group.MapGet("/{poolId:guid}", (Guid poolId, ClaimsPrincipal principal, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var pool = pools.GetPoolForUser(poolId, userId);
            return pool is null ? Results.NotFound() : Results.Ok(pool);
        }).RequireAuthorization();

        group.MapPut("/{poolId:guid}", (Guid poolId, ClaimsPrincipal principal, UpdatePoolRequest request, PoolStore pools, PredictionStore predictions) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var existing = pools.GetPool(poolId);
                if (existing is null)
                {
                    return Results.NotFound();
                }

                var previousStartingBalance = existing.StartingBalance;
                var updated = pools.UpdatePool(poolId, userId, request);
                predictions.ApplyStartingBalanceAdjustment(updated.Id, pools.GetMembers(updated.Id), previousStartingBalance, updated.StartingBalance);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        group.MapPost("/{poolId:guid}/invites", (Guid poolId, ClaimsPrincipal principal, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var invite = pools.CreateInvite(poolId, userId);
                return Results.Created($"/api/pools/invites/{invite.Code}", invite);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        }).RequireAuthorization();

        group.MapGet("/{poolId:guid}/invites", (Guid poolId, ClaimsPrincipal principal, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(pools.GetInvites(poolId, userId));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        }).RequireAuthorization();

        group.MapPost("/{poolId:guid}/invites/{inviteId:guid}/revoke", (Guid poolId, Guid inviteId, ClaimsPrincipal principal, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(pools.RevokeInvite(poolId, inviteId, userId));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        }).RequireAuthorization();

        group.MapPost("/{poolId:guid}/join-requests", async (
            Guid poolId,
            ClaimsPrincipal principal,
            IDbContextFactory<PoolPredictDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            if (!await db.Pools.AnyAsync(pool => pool.Id == poolId, cancellationToken))
            {
                return Results.NotFound();
            }

            if (await db.PoolMembers.AnyAsync(member => member.PoolId == poolId && member.UserId == userId, cancellationToken))
            {
                return Results.BadRequest(new { error = "You are already a member of this pool." });
            }

            var existing = await db.PoolJoinRequests.SingleOrDefaultAsync(
                request => request.PoolId == poolId && request.UserId == userId,
                cancellationToken);

            if (existing is not null)
            {
                if (existing.Status != "Pending")
                {
                    existing.Status = "Pending";
                    existing.RequestedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                }

                return Results.Ok(new JoinRequestResponse(existing.Id, existing.PoolId, existing.Status, existing.RequestedAt));
            }

            var joinRequest = new PersistedPoolJoinRequest
            {
                Id = Ids.NewId(),
                PoolId = poolId,
                UserId = userId,
                RequestedAt = DateTimeOffset.UtcNow,
                Status = "Pending"
            };

            db.PoolJoinRequests.Add(joinRequest);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/pools/{poolId}/join-requests/{joinRequest.Id}", new JoinRequestResponse(joinRequest.Id, joinRequest.PoolId, joinRequest.Status, joinRequest.RequestedAt));
        }).RequireAuthorization();

        group.MapGet("/{poolId:guid}/join-requests", (Guid poolId, ClaimsPrincipal principal, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(pools.GetJoinRequests(poolId, userId));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        }).RequireAuthorization();

        group.MapPost("/{poolId:guid}/join-requests/{requestId:guid}/approve", (Guid poolId, Guid requestId, ClaimsPrincipal principal, PoolStore pools, PredictionStore predictions) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var decision = pools.ApproveJoinRequest(poolId, requestId, userId);
                var pool = pools.GetPool(poolId);
                if (pool is not null && decision.MemberId is Guid memberId)
                {
                    predictions.InitializeMemberBalance(poolId, memberId, pool.StartingBalance);
                }

                return Results.Ok(decision);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        }).RequireAuthorization();

        group.MapPost("/{poolId:guid}/join-requests/{requestId:guid}/deny", (Guid poolId, Guid requestId, ClaimsPrincipal principal, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(pools.DenyJoinRequest(poolId, requestId, userId));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        }).RequireAuthorization();

        group.MapGet("/invites/{code}", (string code, PoolStore pools) =>
        {
            var invite = pools.GetInvite(code);
            return invite is null ? Results.NotFound() : Results.Ok(invite);
        });

        group.MapPost("/join", (ClaimsPrincipal principal, JoinPoolRequest request, PoolStore pools, PredictionStore predictions) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var pool = pools.JoinPool(userId, request);
                predictions.InitializeMemberBalance(pool.Id, pool.MemberId, pool.StartingBalance);
                return Results.Ok(pool);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).RequireAuthorization();

        group.MapGet("/{poolId:guid}/markets", (Guid poolId, ClaimsPrincipal principal, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            if (pools.GetPool(poolId) is null)
            {
                return Results.NotFound();
            }

            return pools.GetMember(poolId, userId) is null
                ? Results.Forbid()
                : Results.Ok(pools.GetMarkets(poolId));
        }).RequireAuthorization();

        return app;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}

public sealed record DiscoverPoolResponse(
    Guid Id,
    string Name,
    string OwnerDisplayName,
    string OwnerEmail,
    string TournamentName,
    string Provider,
    bool IsTestData,
    string Profile,
    int StartingBalance,
    int MemberCount,
    int InviteCount,
    bool HasPendingJoinRequest);

public sealed record JoinRequestResponse(
    Guid Id,
    Guid PoolId,
    string Status,
    DateTimeOffset RequestedAt);
