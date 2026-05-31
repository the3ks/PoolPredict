using PoolPredict.Api.Domain.Identity;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PoolPredict.Api.Modules.Identity;

public sealed class JwtTokenService(IConfiguration configuration)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _issuer = configuration["Authentication:Issuer"] ?? "PoolPredict.Api";
    private readonly string _audience = configuration["Authentication:Audience"] ?? "PoolPredict.Web";
    private readonly byte[] _signingKey = Encoding.UTF8.GetBytes(
        configuration["Authentication:SigningKey"] ?? "dev-only-poolpredict-signing-key-change-before-production");

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(12);
        var header = new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        };

        var payload = new Dictionary<string, object>
        {
            ["iss"] = _issuer,
            ["aud"] = _audience,
            ["sub"] = user.Id.ToString(),
            ["email"] = user.Email,
            ["name"] = user.DisplayName,
            [ClaimTypes.Role] = user.Role.ToString(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var unsignedToken = $"{Encode(header)}.{Encode(payload)}";
        var signature = Sign(unsignedToken);

        return ($"{unsignedToken}.{signature}", expiresAt);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var unsignedToken = $"{parts[0]}.{parts[1]}";
        var expectedSignature = Sign(unsignedToken);
        if (!FixedTimeEquals(expectedSignature, parts[2]))
        {
            return null;
        }

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        using var payload = JsonDocument.Parse(payloadJson);
        var root = payload.RootElement;

        if (!TryGetString(root, "iss", out var issuer) || issuer != _issuer ||
            !TryGetString(root, "aud", out var audience) || audience != _audience ||
            !TryGetString(root, "sub", out var subject) ||
            !root.TryGetProperty("exp", out var expElement))
        {
            return null;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expElement.GetInt64());
        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, subject) };

        if (TryGetString(root, "email", out var email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        if (TryGetString(root, "name", out var name))
        {
            claims.Add(new Claim(ClaimTypes.Name, name));
        }

        if (TryGetString(root, ClaimTypes.Role, out var role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, JwtBearerAuthenticationHandler.SchemeName));
    }

    private string Sign(string unsignedToken)
    {
        using var hmac = new HMACSHA256(_signingKey);
        return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedToken)));
    }

    private static string Encode<T>(T value) => Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Convert.FromBase64String(base64);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.ASCII.GetBytes(left);
        var rightBytes = Encoding.ASCII.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        if (root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? "";
            return true;
        }

        value = "";
        return false;
    }
}
