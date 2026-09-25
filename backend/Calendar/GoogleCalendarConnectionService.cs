using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Data;
using backend.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace backend.Calendar;

public interface IGoogleCalendarConnectionService
{
    string CreateAuthorizationUrl(string userId);
    Task CompleteAuthorizationAsync(string state, string code, CancellationToken ct);
    Task<string?> GetAccessTokenAsync(string userId, CancellationToken ct);
    Task DisconnectAsync(string userId, CancellationToken ct);
    Task<bool> IsConnectedAsync(string userId, CancellationToken ct);
}

public sealed class GoogleCalendarConnectionService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IDataProtectionProvider protectionProvider) : IGoogleCalendarConnectionService
{
    private const string Provider = "Google";
    private readonly IDataProtector protector = protectionProvider.CreateProtector("RisePlan.GoogleCalendarConnection.v1");

    public string CreateAuthorizationUrl(string userId)
    {
        var clientId = Required("Google:ClientId");
        var redirectUri = Required("Google:RedirectUri");
        var state = protector.Protect(JsonSerializer.Serialize(new OAuthState(userId, Guid.NewGuid().ToString("N"))));
        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "https://www.googleapis.com/auth/calendar",
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state
        };
        return "https://accounts.google.com/o/oauth2/v2/auth?" +
            string.Join("&", query.Select(item => $"{item.Key}={Uri.EscapeDataString(item.Value)}"));
    }

    public async Task CompleteAuthorizationAsync(string state, string code, CancellationToken ct)
    {
        var oauthState = JsonSerializer.Deserialize<OAuthState>(protector.Unprotect(state))
            ?? throw new InvalidOperationException("The Google authorization state is invalid.");
        var client = httpClientFactory.CreateClient("GoogleOAuth");
        using var response = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = Required("Google:ClientId"),
                ["client_secret"] = Required("Google:ClientSecret"),
                ["redirect_uri"] = Required("Google:RedirectUri"),
                ["grant_type"] = "authorization_code"
            }),
            ct);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Google returned an empty token response.");
        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new InvalidOperationException("Google returned no access token.");

        var connection = await db.CalendarConnections
            .SingleOrDefaultAsync(item => item.UserId == oauthState.UserId && item.Provider == Provider, ct);
        if (connection is null)
        {
            connection = new CalendarConnection
            {
                UserId = oauthState.UserId,
                Provider = Provider,
                AccessTokenEncrypted = protector.Protect(token.AccessToken),
                RefreshTokenEncrypted = token.RefreshToken is null ? null : protector.Protect(token.RefreshToken),
                TokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)
            };
            db.CalendarConnections.Add(connection);
        }
        else
        {
            connection.AccessTokenEncrypted = protector.Protect(token.AccessToken);
            connection.RefreshTokenEncrypted ??= token.RefreshToken is null ? null : protector.Protect(token.RefreshToken);
            connection.TokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
            connection.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetAccessTokenAsync(string userId, CancellationToken ct)
    {
        var connection = await db.CalendarConnections
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Provider == Provider, ct);
        if (connection is null)
            return null;
        if (connection.TokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return protector.Unprotect(connection.AccessTokenEncrypted);
        if (connection.RefreshTokenEncrypted is null)
            return null;

        var client = httpClientFactory.CreateClient("GoogleOAuth");
        using var response = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = Required("Google:ClientId"),
                ["client_secret"] = Required("Google:ClientSecret"),
                ["refresh_token"] = protector.Unprotect(connection.RefreshTokenEncrypted),
                ["grant_type"] = "refresh_token"
            }),
            ct);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Google returned an empty refresh response.");
        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new InvalidOperationException("Google returned no refreshed access token.");
        connection.AccessTokenEncrypted = protector.Protect(token.AccessToken);
        connection.TokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
        connection.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return token.AccessToken;
    }

    public async Task DisconnectAsync(string userId, CancellationToken ct)
    {
        var connection = await db.CalendarConnections
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Provider == Provider, ct);
        if (connection is not null)
        {
            db.CalendarConnections.Remove(connection);
            await db.SaveChangesAsync(ct);
        }
    }

    public Task<bool> IsConnectedAsync(string userId, CancellationToken ct) =>
        db.CalendarConnections.AnyAsync(item => item.UserId == userId && item.Provider == Provider, ct);

    private string Required(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key} is not configured.");

    private sealed record OAuthState(string UserId, string Nonce);
    private sealed record GoogleTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
