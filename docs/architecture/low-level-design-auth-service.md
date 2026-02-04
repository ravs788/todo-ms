# Low-Level Design (LLD) - Authentication Service

**Version:** 1.0  
**Date:** March 2, 2026  
**Status:** Draft

## Overview

The Authentication Service is responsible for user authentication, JWT token lifecycle management, password reset workflows, and JWKS endpoint for public key distribution. It's built with ASP.NET Core 8 and follows secure authentication best practices.

## Directory Structure

```
services/auth-service/
├── AuthService.API/
│   ├── Controllers/
│   │   └── AuthController.cs
│   ├── Filters/
│   │   └── CorrelationIdFilter.cs
│   ├── Middleware/
│   │   ├── ExceptionHandlingMiddleware.cs
│   │   └── CorrelationIdMiddleware.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
├── AuthService.Core/
│   ├── DTOs/
│   │   ├── LoginRequest.cs
│   │   ├── RegisterRequest.cs
│   │   ├── TokenResponse.cs
│   │   └── ForgotPasswordRequest.cs
│   ├── Entities/
│   │   ├── User.cs
│   │   ├── RefreshToken.cs
│   │   └── PasswordResetLog.cs
│   ├── Interfaces/
│   │   ├── ITokenService.cs
│   │   ├── IJwksService.cs
│   │   ├── IAuthRepository.cs
│   │   └── IPasswordHasher.cs
│   └── Services/
│       ├── TokenService.cs
│       ├── JwksService.cs
│       └── PasswordService.cs
├── AuthService.Infrastructure/
│   ├── Data/
│   │   └── AuthDbContext.cs
│   ├── Migrations/
│   ├── Repositories/
│   │   └── AuthRepository.cs
│   └── Security/
│       ├── RsaKeyProvider.cs
│       └── KeyRotationService.cs
├── AuthService.Tests/
│   ├── Unit/
│   └── Integration/
└── Dockerfile
```

## Core Models

### User
- **Fields:** Id, Username, Email, PasswordHash, Approved, Roles[], CreatedAt, UpdatedAt
- **Constraints:** Username and Email must be unique
- **Index:** Username, Email

### RefreshToken
- **Fields:** Id, UserId, Token, ExpiresAt, RevokedAt
- **Relationship:** Many-to-One with User
- **Constraints:** Token must be unique per user

### PasswordResetLog
- **Fields:** Id, UserId, ResetAt, IpAddress, UserAgent
- **Relationship:** Many-to-One with User

## Key Endpoints

| Method | Endpoint | Description | Request Body | Response Body |
|--------|----------|-------------|--------------|---------------|
| POST | /api/v1/auth/register | User registration | RegisterRequest | TokenResponse |
| POST | /api/v1/auth/login | User login | LoginRequest | TokenResponse |
| POST | /api/v1/auth/refresh | Refresh access token | { refreshToken: string } | TokenResponse |
| POST | /api/v1/auth/logout | Revoke refresh token | { refreshToken: string } | { success: boolean } |
| POST | /api/v1/auth/forgot-password | Initiate password reset | ForgotPasswordRequest | { success: boolean } |
| GET | /api/v1/auth/jwks | Public JWKS endpoint | None | JwksResponse |

### Request/Response Schemas

**LoginRequest**
```json
{
  "username": "string",
  "password": "string"
}
```

**TokenResponse**
```json
{
  "accessToken": "string",
  "refreshToken": "string",
  "expiresIn": 900,
  "tokenType": "Bearer"
}
```

**JwksResponse**
```json
{
  "keys": [
    {
      "kid": "string",
      "kty": "RSA",
      "alg": "RS256",
      "use": "sig",
      "n": "string",
      "e": "string"
    }
  ]
}
```

## Services

### TokenService
- **Responsibilities:**
  - Generate JWT access tokens with RS256 signing
  - Generate and manage refresh tokens
  - Validate and revoke refresh tokens
- **Key Methods:**
  - `GenerateTokens(user, jti?)`: TokenResponse
  - `RevokeRefreshToken(userId, token)`: boolean

### JwksService
- **Responsibilities:**
  - Provide public RSA keys for JWT verification
  - Handle key rotation and caching
- **Key Methods:**
  - `GetJwksJson()`: string

### PasswordService
- **Responsibilities:**
  - Hash passwords with BCrypt (work factor 12+)
  - Verify passwords against hashes
- **Key Methods:**
  - `HashPassword(password)`: string
  - `VerifyPassword(password, hash)`: boolean

## Configuration

### Key Settings
- **Jwt:**
  - Issuer: "http://auth-service:5001"
  - Audience: "todo-api"
  - AccessTokenLifetimeMinutes: 15
  - RefreshTokenLifetimeDays: 7
  - KeyRotationDays: 90
- **ConnectionStrings:**
  - AuthConnection: PostgreSQL connection string
- **Logging:** Standard ASP.NET Core logging

### Environment Variables
- **ConnectionStrings__AuthConnection:** Database connection string
- **Jwt__Issuer:** JWT issuer
- **Jwt__Audience:** JWT audience
- **JWT_PRIVATE_KEY:** Private key for signing (PEM format)

## Security

### Password Hashing
- **Algorithm:** BCrypt with work factor 12+
- **Storage:** Securely hashed in database
- **Verification:** Always verify against hash

### Token Security
- **Access Token:** Short-lived (15 minutes)
- **Refresh Token:** Long-lived (7 days), rotated on use
- **JWT:** RS256 asymmetric signing
- **JWKS:** Cached downstream for performance

### Key Rotation
- **Rotation:** Every 90 days
- **Grace Period:** Overlapping key period
- **Automatic:** KeyRotationService handles rotation

## Error Handling

### Standard Error Response
```json
{
  "traceId": "uuid-or-correlation",
  "timestamp": "ISO-8601",
  "status": 400,
  "error": "Bad Request",
  "code": "AUTH_ERROR",
  "message": "Error message"
}
```

### Custom Exceptions
- **AuthException:** Base authentication exception
- **UserNotFoundException:** User not found
- **InvalidCredentialsException:** Invalid credentials
- **DuplicateUserException:** Username/email already exists

## Testing

### Unit Tests
- **TokenService:** Test token generation and validation
- **JwksService:** Test JWKS key generation
- **PasswordService:** Test password hashing and verification

### Integration Tests
- **Endpoint Testing:** Test all API endpoints with real database
- **Token Flow:** Test login, refresh, and logout flow
- **Key Rotation:** Test key rotation process

## Deployment

### Container Configuration
- **Base Image:** mcr.microsoft.com/dotnet/aspnet:8.0
- **Port:** 5001
- **Health Checks:** /health endpoint

### Kubernetes Considerations
- **Replicas:** 2-3 instances for availability
- **Resources:** Moderate CPU/memory usage
- **Secrets:** JWT private keys stored as Kubernetes Secrets

## Monitoring

### Metrics
- `auth_requests_total`: Total authentication requests
- `auth_errors_total`: Authentication errors by type
- `token_generation_duration_ms`: Token generation time
- `password_hash_duration_ms`: Password hashing time

### Logging
- Successful/failed login attempts
- Token refresh attempts
- Password reset requests
- Key rotation events

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-02 | Architecture Team | Initial LLD for Auth Service |
