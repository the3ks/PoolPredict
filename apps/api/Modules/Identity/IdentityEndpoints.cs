using System.Security.Claims;

namespace PoolPredict.Api.Modules.Identity;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", (RegisterRequest request, IdentityStore users, JwtTokenService tokens) =>
        {
            try
            {
                return Results.Created("/api/auth/me", CreateAuthResponse(users.Register(request), tokens));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        group.MapPost("/login", (LoginRequest request, IdentityStore users, JwtTokenService tokens) =>
        {
            try
            {
                return Results.Ok(CreateAuthResponse(users.Login(request), tokens));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/google", (GoogleLoginRequest request, IdentityStore users, JwtTokenService tokens) =>
        {
            try
            {
                return Results.Ok(CreateAuthResponse(users.LoginWithGoogle(request), tokens));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/me", (ClaimsPrincipal principal, IdentityStore users) =>
        {
            var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(subject, out var userId))
            {
                return Results.Unauthorized();
            }

            var user = users.GetUser(userId);
            return user is null ? Results.Unauthorized() : Results.Ok(user.ToProfileResponse());
        }).RequireAuthorization();

        return app;
    }

    private static AuthResponse CreateAuthResponse(Domain.Identity.User user, JwtTokenService tokens)
    {
        var token = tokens.CreateToken(user);
        return new AuthResponse(token.Token, token.ExpiresAt, user.ToProfileResponse());
    }

    private static UserProfileResponse ToProfileResponse(this Domain.Identity.User user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role);
}
