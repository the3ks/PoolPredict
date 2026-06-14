using System.Security.Claims;
using PoolPredict.Api.Modules.Email;

namespace PoolPredict.Api.Modules.Identity;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", (RegisterRequest request, IdentityStore users) =>
        {
            try
            {
                users.Register(request);
                return Results.Created(
                    "/api/admin/users",
                    new AuthMessageResponse("Account created. Your account is pending admin activation before you can log in."));
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
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
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

        group.MapPost("/verify-email", (VerifyEmailRequest _, IdentityStore __) =>
        {
            return Results.BadRequest(new { error = "Self-activation is disabled. A platform admin must activate your account." });
        });

        group.MapPost("/resend-verification", (ResendVerificationRequest _, IdentityStore __) =>
        {
            return Results.BadRequest(new { error = "Self-activation is disabled. Contact a platform admin to activate your account." });
        });

        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, IdentityStore users, SmtpEmailSender emailSender, IConfiguration configuration) =>
        {
            try
            {
                var user = users.FindByEmail(request.Email);
                if (user is not null)
                {
                    var token = users.CreatePasswordResetToken(user.Id);
                    var link = BuildWebLink(configuration, "/reset-password", token);
                    await emailSender.SendAsync(
                        user.Email,
                        "Reset your PoolPredict password",
                        $"Reset your PoolPredict password by opening this link: {link}");
                }
            }
            catch (ArgumentException)
            {
                // Keep the public response account-enumeration safe.
            }

            return Results.Ok(new AuthMessageResponse("If an account exists for this email, a reset link will be sent."));
        });

        group.MapPost("/reset-password", (ResetPasswordRequest request, IdentityStore users) =>
        {
            try
            {
                return users.ResetPassword(request)
                    ? Results.Ok(new AuthMessageResponse("Password reset. You can now log in."))
                    : Results.BadRequest(new { error = "Reset link is invalid or expired." });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/change-password", (ChangePasswordRequest request, ClaimsPrincipal principal, IdentityStore users) =>
        {
            var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(subject, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                users.ChangePassword(userId, request);
                return Results.Ok(new AuthMessageResponse("Password changed."));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }).RequireAuthorization();

        group.MapPut("/me", (UpdateProfileRequest request, ClaimsPrincipal principal, IdentityStore users) =>
        {
            var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(subject, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(users.UpdateProfile(userId, request).ToProfileResponse());
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }).RequireAuthorization();

        group.MapPost("/me/avatar", async (IFormFile avatar, ClaimsPrincipal principal, IdentityStore users, AvatarService avatars, CancellationToken cancellationToken) =>
        {
            var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(subject, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var avatarUrl = await avatars.SaveAvatarAsync(userId, avatar, cancellationToken);
                return Results.Ok(users.UpdateAvatarUrl(userId, avatarUrl).ToProfileResponse());
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }).RequireAuthorization().DisableAntiforgery();

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
        new(user.Id, user.Email, user.DisplayName, user.AvatarUrl, user.Role, user.IsEmailVerified, user.MustChangePassword);

    private static string BuildWebLink(IConfiguration configuration, string path, string token)
    {
        var baseUrl = (configuration["WebApp:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        return $"{baseUrl}{path}?token={Uri.EscapeDataString(token)}";
    }
}
