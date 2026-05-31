namespace PoolPredict.Api.Modules.Identity;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
