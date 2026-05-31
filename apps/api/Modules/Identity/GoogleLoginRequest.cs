namespace PoolPredict.Api.Modules.Identity;

public sealed record GoogleLoginRequest(string Email, string DisplayName, string GoogleSubject);
