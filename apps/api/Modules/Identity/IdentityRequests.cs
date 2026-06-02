namespace PoolPredict.Api.Modules.Identity;

public sealed record AuthMessageResponse(string Message);

public sealed record VerifyEmailRequest(string Token);

public sealed record ResendVerificationRequest(string Email);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
