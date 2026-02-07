namespace AuthService.Models
{
    /// <summary>
    /// Login payload for dev thin slice; accepts any non-empty username/password.
    /// </summary>
    public record LoginRequest(string Username, string Password);

    /// <summary>
    /// Authentication response containing short-lived access token and opaque refresh token.
    /// </summary>
    /// <param name="AccessToken">Signed RS256 JWT.</param>
    /// <param name="RefreshToken">Opaque token for obtaining new access tokens.</param>
    /// <param name="ExpiresIn">Seconds until access token expiry.</param>
    /// <param name="TokenType">Defaults to "Bearer".</param>
    public record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType = "Bearer");

    /// <summary>
    /// Request to exchange a refresh token for a new access/refresh pair.
    /// </summary>
    public record RefreshRequest(string RefreshToken);

    /// <summary>
    /// Registration payload to create a new user.
    /// </summary>
    public record RegisterRequest(string Username, string Password);

    /// <summary>
    /// Logout request to revoke a refresh token.
    /// </summary>
    public record LogoutRequest(string RefreshToken);
}
