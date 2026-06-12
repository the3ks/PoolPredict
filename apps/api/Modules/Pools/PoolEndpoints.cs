using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Pools;
using PoolPredict.Api.Infrastructure.Persistence;
using PoolPredict.Api.Modules.Email;
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
                where !pool.IsHidden &&
                    !db.PoolMembers.Any(member => member.PoolId == pool.Id && member.UserId == userId)
                select new
                {
                    pool.Id,
                    pool.Name,
                    pool.CoverImageUrl,
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
                pool.CoverImageUrl,
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
                where !pool.IsHidden
                select new
                {
                    pool.Id,
                    pool.Name,
                    pool.CoverImageUrl,
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
                pool.CoverImageUrl,
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

        group.MapGet("/{poolId:guid}/messages", async (
            Guid poolId,
            ClaimsPrincipal principal,
            PoolStore pools,
            IDbContextFactory<PoolPredictDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            if (pools.GetPool(poolId) is null)
            {
                return Results.NotFound();
            }

            if (pools.GetMember(poolId, userId) is null)
            {
                return Results.Forbid();
            }

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var announcements = await (
                from message in db.PoolMessages.AsNoTracking()
                join member in db.PoolMembers.AsNoTracking()
                    on message.AuthorMemberId equals member.Id
                join user in db.Users.AsNoTracking()
                    on member.UserId equals user.Id
                where message.PoolId == poolId
                    && message.Kind == PoolMessageKind.Announcement
                    && message.AnnouncementSlot != null
                orderby message.AnnouncementSlot
                select new PoolMessageResponse(
                    message.Id,
                    message.PoolId,
                    message.AuthorMemberId,
                    user.DisplayName,
                    user.AvatarUrl,
                    member.Role,
                    message.Kind,
                    message.AnnouncementSlot,
                    message.Title,
                    message.BodyMarkdown,
                    message.CreatedAt,
                    message.EditedAt))
                .ToArrayAsync(cancellationToken);

            var chatRows = await (
                from message in db.PoolMessages.AsNoTracking()
                join member in db.PoolMembers.AsNoTracking()
                    on message.AuthorMemberId equals member.Id
                join user in db.Users.AsNoTracking()
                    on member.UserId equals user.Id
                where message.PoolId == poolId
                    && message.Kind == PoolMessageKind.Chat
                orderby message.CreatedAt descending
                select new PoolMessageResponse(
                    message.Id,
                    message.PoolId,
                    message.AuthorMemberId,
                    user.DisplayName,
                    user.AvatarUrl,
                    member.Role,
                    message.Kind,
                    message.AnnouncementSlot,
                    message.Title,
                    message.BodyMarkdown,
                    message.CreatedAt,
                    message.EditedAt))
                .Take(100)
                .ToArrayAsync(cancellationToken);

            return Results.Ok(announcements.Concat(chatRows.Reverse()));
        }).RequireAuthorization();

        group.MapPost("/{poolId:guid}/messages", async (
            Guid poolId,
            ClaimsPrincipal principal,
            CreatePoolMessageRequest request,
            PoolStore pools,
            IDbContextFactory<PoolPredictDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            if (pools.GetPool(poolId) is null)
            {
                return Results.NotFound();
            }

            var member = pools.GetMember(poolId, userId);
            if (member is null)
            {
                return Results.Forbid();
            }

            if (request.Kind == PoolMessageKind.Announcement)
            {
                return Results.BadRequest(new { error = "Use an announcement slot update endpoint." });
            }

            var body = request.BodyMarkdown?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(body))
            {
                return Results.BadRequest(new { error = "Message body is required." });
            }

            if (body.Length > 4000)
            {
                return Results.BadRequest(new { error = "Message body must be 4,000 characters or fewer." });
            }

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var message = new PersistedPoolMessage
            {
                Id = Ids.NewId(),
                PoolId = poolId,
                AuthorMemberId = member.Id,
                Kind = request.Kind,
                Title = "",
                BodyMarkdown = body,
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.PoolMessages.Add(message);
            await db.SaveChangesAsync(cancellationToken);

            var author = await db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
            var response = new PoolMessageResponse(
                message.Id,
                message.PoolId,
                message.AuthorMemberId,
                author?.DisplayName ?? "Pool member",
                author?.AvatarUrl,
                member.Role,
                message.Kind,
                message.AnnouncementSlot,
                message.Title,
                message.BodyMarkdown,
                message.CreatedAt,
                message.EditedAt);

            return Results.Created($"/api/pools/{poolId}/messages/{message.Id}", response);
        }).RequireAuthorization();

        group.MapPut("/{poolId:guid}/announcements/{slot:int}", async (
            Guid poolId,
            int slot,
            ClaimsPrincipal principal,
            UpdatePoolAnnouncementRequest request,
            PoolStore pools,
            IDbContextFactory<PoolPredictDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            if (slot is not (1 or 2))
            {
                return Results.BadRequest(new { error = "Announcement slot must be 1 or 2." });
            }

            if (pools.GetPool(poolId) is null)
            {
                return Results.NotFound();
            }

            var member = pools.GetMember(poolId, userId);
            if (member is null)
            {
                return Results.Forbid();
            }

            if (member.Role is not PoolMemberRole.Owner)
            {
                return Results.Forbid();
            }

            var title = request.Title?.Trim() ?? "";
            var body = request.BodyMarkdown?.Trim() ?? "";
            if (title.Length > 200)
            {
                return Results.BadRequest(new { error = "Announcement title must be 200 characters or fewer." });
            }

            if (body.Length > 4000)
            {
                return Results.BadRequest(new { error = "Announcement content must be 4,000 characters or fewer." });
            }

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var announcement = await db.PoolMessages.SingleOrDefaultAsync(
                message => message.PoolId == poolId
                    && message.Kind == PoolMessageKind.Announcement
                    && message.AnnouncementSlot == slot,
                cancellationToken);

            var now = DateTimeOffset.UtcNow;
            if (announcement is null)
            {
                announcement = new PersistedPoolMessage
                {
                    Id = Ids.NewId(),
                    PoolId = poolId,
                    AuthorMemberId = member.Id,
                    Kind = PoolMessageKind.Announcement,
                    AnnouncementSlot = slot,
                    CreatedAt = now
                };
                db.PoolMessages.Add(announcement);
            }

            announcement.AuthorMemberId = member.Id;
            announcement.Title = title;
            announcement.BodyMarkdown = body;
            announcement.EditedAt = now;
            await db.SaveChangesAsync(cancellationToken);

            var author = await db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
            return Results.Ok(new PoolMessageResponse(
                announcement.Id,
                announcement.PoolId,
                announcement.AuthorMemberId,
                author?.DisplayName ?? "Pool owner",
                author?.AvatarUrl,
                member.Role,
                announcement.Kind,
                announcement.AnnouncementSlot,
                announcement.Title,
                announcement.BodyMarkdown,
                announcement.CreatedAt,
                announcement.EditedAt));
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

        group.MapPut("/{poolId:guid}/members/{memberId:guid}/leaderboard-status", (
            Guid poolId,
            Guid memberId,
            ClaimsPrincipal principal,
            UpdateMemberLeaderboardStatusRequest request,
            PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var member = pools.UpdateMemberLeaderboardStatus(poolId, userId, memberId, request.LeaderboardStatus);
                return Results.Ok(new PoolMemberLeaderboardStatusResponse(member.Id, member.LeaderboardStatus));
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
            SmtpEmailSender emailSender,
            IConfiguration configuration,
            IDbContextFactory<PoolPredictDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var notification = await (
                from pool in db.Pools.AsNoTracking()
                join owner in db.Users.AsNoTracking()
                    on pool.OwnerUserId equals owner.Id
                join requester in db.Users.AsNoTracking()
                    on userId equals requester.Id
                where pool.Id == poolId && !pool.IsHidden
                select new JoinRequestNotification(
                    pool.Id,
                    pool.Name,
                    owner.Email,
                    owner.DisplayName,
                    requester.Email,
                    requester.DisplayName))
                .SingleOrDefaultAsync(cancellationToken);

            if (notification is null)
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
                    await SendJoinRequestNotificationAsync(emailSender, configuration, notification, cancellationToken);
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
            await SendJoinRequestNotificationAsync(emailSender, configuration, notification, cancellationToken);
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

    private static Task<EmailSendResult> SendJoinRequestNotificationAsync(
        SmtpEmailSender emailSender,
        IConfiguration configuration,
        JoinRequestNotification notification,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<EmailSendResult>(cancellationToken);
        }

        var requesterName = string.IsNullOrWhiteSpace(notification.RequesterDisplayName)
            ? notification.RequesterEmail
            : notification.RequesterDisplayName;
        var ownerName = string.IsNullOrWhiteSpace(notification.OwnerDisplayName)
            ? "Pool owner"
            : notification.OwnerDisplayName;
        var poolLink = BuildPoolLink(configuration, notification.PoolId);
        var body =
            $"Hi {ownerName},\n\n" +
            $"{requesterName} ({notification.RequesterEmail}) requested to join your pool \"{notification.PoolName}\".\n\n" +
            $"Review the request here: {poolLink}\n\n" +
            "PoolPredict";

        return emailSender.SendAsync(
            notification.OwnerEmail,
            $"New join request for {notification.PoolName}",
            body);
    }

    private static string BuildPoolLink(IConfiguration configuration, Guid poolId)
    {
        var baseUrl = (configuration["WebApp:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        return $"{baseUrl}/pools/{poolId}";
    }
}

public sealed record DiscoverPoolResponse(
    Guid Id,
    string Name,
    string? CoverImageUrl,
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

public sealed record JoinRequestNotification(
    Guid PoolId,
    string PoolName,
    string OwnerEmail,
    string OwnerDisplayName,
    string RequesterEmail,
    string RequesterDisplayName);

public sealed record CreatePoolMessageRequest(
    PoolMessageKind Kind,
    string BodyMarkdown);

public sealed record UpdatePoolAnnouncementRequest(
    string? Title,
    string? BodyMarkdown);

public sealed record PoolMessageResponse(
    Guid Id,
    Guid PoolId,
    Guid AuthorMemberId,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    PoolMemberRole AuthorRole,
    PoolMessageKind Kind,
    int? AnnouncementSlot,
    string Title,
    string BodyMarkdown,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt);
