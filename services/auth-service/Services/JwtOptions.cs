using System;

namespace AuthService.Services
{
    public class JwtOptions
    {
        public string Issuer { get; set; } = "todo-ms-auth";
        public string Audience { get; set; } = "todo-ms-clients";
        public int AccessTokenMinutes { get; set; } = 15;
    }
}
