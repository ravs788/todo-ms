namespace AuthService.Models
{
    public record LoginRequest(string Username, string Password);
    public record LoginResponse(string AccessToken, int ExpiresIn, string TokenType = "Bearer");
}
