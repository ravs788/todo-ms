# Low-Level Design (LLD) - Authentication Service

**Version:** 1.0  
**Date:** February 5, 2026  
**Status:** Draft

## Overview

The Authentication Service handles user authentication, JWT token management, and registration workflows. It's built with .NET 8 and follows clean architecture principles with Entity Framework Core for data access.

## Current Thin Slice (Local)
- Endpoints implemented: POST /api/v1/auth/register, POST /api/v1/auth/login, POST /api/v1/auth/refresh, POST /api/v1/auth/logout, GET /api/v1/auth/jwks, GET /health, GET /api/v1/auth/health
- Behavior: register creates user with PBKDF2-hashed password; login verifies credentials and issues RS256 JWT (kid set) plus refreshToken; includes approved=true and roles=["USER"]; refresh validates stored refresh token, rotates it and revokes the old one; logout revokes the provided refresh token; health endpoints return {"status":"healthy"}
- Persistence: PostgreSQL (docker compose postgres-auth). Entities: users, refresh_tokens. Database EnsureCreated in Development for the thin slice.
- Keys: Ephemeral RSA keypair generated on startup; JWKS exposes public key
- Hosting: http://0.0.0.0:5001 (bound for Kong reachability); routed via Kong at http://localhost:8000/api/v1/auth/*
- Verified: curl direct and via Kong return 200 for health/jwks and successful flows for register/login/refresh/logout
- Next: add rate limiting, email-based flows (forgot/reset password), key rotation, tests/coverage

## Directory Structure

```
services/auth-service/
├── src/
│   ├── Auth.Api/                           # ASP.NET Core Web API
│   │   ├── Program.cs
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs
│   │   │   └── AuthController.cs
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   └── CorrelationMiddleware.cs
│   │   └── Program.cs
│   ├── Auth.Application/                  # Application Layer
│   │   ├── Services/
│   │   │   ├── IAuthService.cs
│   │   │   ├── UserService.cs
│   │   │   ├── JwtService.cs
│   │   │   └── TokenValidationService.cs
│   │   ├── DTOs/
│   │   │   ├── LoginRequest.cs
│   │   │   ├── RegisterRequest.cs
│   │   │   ├── AuthResponse.cs
│   │   │   └── ErrorResponse.cs
│   │   └── Interfaces/
│   │       └── IUnitOfWork.cs
│   ├── Auth.Infrastructure/               # Infrastructure Layer
│   │   ├── Data/
│   │   │   ├── AuthDbContext.cs
│   │   │   ├── Migrations/
│   │   │   └── Repositories/
│   │   │       ├── IUserRepository.cs
│   │   │       ├── IJwtRepository.cs
│   │   │       ├── UserRepository.cs
│   │   │       └── RefreshTokenRepository.cs
│   │   ├── Services/
│   │   │   ├── IJwtGenerator.cs
│   │   │   ├── IJwtValidator.cs
│   │   │   ├── JwtGenerator.cs
│   │   │   └── JwtValidator.cs
│   │   └── External/
│   │       └── IEmailService.cs
│   └── Auth.Domain/                        # Domain Layer
│       ├── Entities/
│       │   ├── User.cs
│       │   ├── RefreshToken.cs
│       │   └── Role.cs
│       ├── Interfaces/
│       │   ├── IUserRepository.cs
│       │   ├── IRefreshTokenRepository.cs
│       │   └── IJwtRepository.cs
│       └── ValueObjects/
│           └── Email.cs
│
├── tests/                                  # Test Projects
│   ├── Auth.Api.Tests/
│   ├── Auth.Application.Tests/
│   └── Auth.Integration.Tests/
│
├── Dockerfile
└── README.md
```

## Core Models

### User
- **Fields:** Id, Email, PasswordHash, Salt, FirstName, LastName, Status, Role, CreatedAt, UpdatedAt
- **Constraints:** Email must be unique, PasswordHash stored as hash + salt
- **Indexes:** Email unique index
- **Relationships:** One-to-Many with RefreshToken

### RefreshToken
- **Fields:** Id, UserId, Token, ExpiresAt, CreatedAt, Revoked
- **Constraints:** Token must be unique, Foreign key to User
- **Indexes:** Token unique index, UserId index
- **Purpose:** Token refresh and invalidation

### Role
- **Fields:** Id, Name, NormalizedName
- **Constraints:** Name must be unique
- **Purpose:** Role-based access control

## Key Endpoints

Note: Implemented now: /api/v1/auth/register, /api/v1/auth/login, /api/v1/auth/refresh, /api/v1/auth/logout, /api/v1/auth/jwks, GET /health, GET /api/v1/auth/health. Remaining endpoints are planned for subsequent slices (e.g., forgot/reset password).

| Method | Endpoint | Description | Access | Request Body | Response Body |
|--------|----------|-------------|---------|--------------|---------------|
| POST | /api/v1/auth/register | User registration | PUBLIC | RegisterRequest | AuthResponse |
| POST | /api/v1/auth/login | User login | PUBLIC | LoginRequest | AuthResponse |
| POST | /api/v1/auth/refresh | Refresh token | PUBLIC | RefreshTokenRequest | AuthResponse |
| POST | /api/v1/auth/logout | Logout (invalidate token) | USER | None | SuccessResponse |
| GET | /api/v1/auth/jwks | JWKS public keys | PUBLIC | None | JSON Web Key Set |
| POST | /api/v1/auth/forgot-password | Request password reset | PUBLIC | ForgotPasswordRequest | SuccessResponse |
| POST | /api/v1/auth/reset-password | Reset password | PUBLIC | ResetPasswordRequest | SuccessResponse |

### Request/Response Schemas

**RegisterRequest**
```json
{
  "email": "string",
  "password": "string",
  "firstName": "string",
  "lastName": "string"
}
```

**LoginRequest**
```json
{
  "email": "string",
  "password": "string"
}
```

**AuthResponse**
```json
{
  "accessToken": "string",
  "refreshToken": "string",
  "expiresIn": 3600,
  "tokenType": "Bearer",
  "user": {
    "id": "uuid",
    "email": "string",
    "firstName": "string",
    "lastName": "string",
    "role": "USER|ADMIN"
  }
}
```

**JWKS Response**
```json
{
  "keys": [
    {
      "kty": "RSA",
      "kid": "auth-service-key-1",
      "use": "sig",
      "alg": "RS256",
      "n": "string",
      "e": "AQAB"
    }
  ]
}
```

## Services

### AuthService
- **Responsibilities:**
  - Handle user authentication and registration
  - Manage JWT token generation and validation
  - Coordinate refresh token management
- **Key Methods:**
  - `RegisterUser(request)`: AuthResponse
  - `AuthenticateUser(email, password)`: AuthResponse
  - `RefreshToken(refreshToken)`: AuthResponse
  - `InvalidateToken(token)`: bool
  - `ValidateToken(token)`: ClaimsPrincipal

### JwtService
- **Responsibilities:**
  - Generate JWT access tokens
  - Generate refresh tokens
  - Validate JWT signatures
  - Manage RSA key pairs
- **Key Methods:**
  - `GenerateAccessToken(user)`: string
  - `GenerateRefreshToken()`: string
  - `ValidateToken(token)`: ClaimsPrincipal
  - `GetJwks()`: JsonWebKeySet

### UserService
- **Responsibilities:**
  - User management operations
  - Password hashing and verification
  - User status management
- **Key Methods:**
  - `CreateUser(request)`: User
  - `GetUserByEmail(email)`: User
  - `HashPassword(password)`: (string, string)
  - `VerifyPassword(password, hash, salt)`: bool

## Configuration

### Key Settings (appsettings.json)
```json
{
  "Jwt": {
    "Issuer": "todo-app",
    "Audience": "todo-app-clients",
    "KeyRotationDays": 90,
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AuthDb;User Id=postgres;Password=secret;"
  },
  "Email": {
    "SmtpServer": "localhost",
    "SmtpPort": 587,
    "SmtpUsername": "noreply@todo-app.com",
    "SmtpPassword": "secret"
  }
}
```

### Environment Variables
- **ConnectionStrings__DefaultConnection:** PostgreSQL connection string
- **JWT__ISSUER:** JWT issuer
- **JWT__AUDIENCE:** JWT audience
- **JWT__KEY_ROTATION_DAYS:** Key rotation period
- **EMAIL__SMTP_*:** SMTP configuration for emails

## Security

### Password Security
- **Hashing Algorithm:** PBKDF2 with HMAC-SHA256
- **Salt:** Unique salt for each user
- **Iterations:** 10,000 iterations
- **Key Size:** 256 bits

### JWT Security
- **Algorithm:** RS256 (RSA with SHA-256)
- **Key Rotation:** Automatic key rotation every 90 days
- **Claims:** Standard JWT claims plus custom ones
- **Expiration:** Access tokens expire in 1 hour
- **Refresh Tokens:** Long-lived tokens with expiration

### Rate Limiting
- **Login Endpoint:** 5 attempts per minute
- **Registration:** 10 attempts per hour
- **Password Reset:** 3 attempts per hour

## Error Handling

### Standard Error Response
```json
{
  "timestamp": "2026-02-05T00:00:00.000Z",
  "status": 400,
  "error": "Bad Request",
  "message": "Validation failed",
  "path": "/api/v1/auth/register",
  "traceId": "uuid"
}
```

### Custom Exceptions
- **AuthException:** Base authentication exception
- **UserNotFoundException:** User not found
- **InvalidCredentialsException:** Invalid login credentials
- **TokenValidationException:** Token validation failed
- **RateLimitExceededException:** Rate limit exceeded

### Exception Handling
- **Middleware:** Global exception handling middleware
- **Logging:** All exceptions logged with correlation ID
- **Validation:** FluentValidation for input validation

## Testing

### Unit Tests
- **AuthService:** Test authentication and token management
- **JwtService:** Test JWT generation and validation
- **UserService:** Test user management and password hashing

### Integration Tests
- **Endpoint Testing:** Test all API endpoints
- **Database Testing:** Test data persistence with TestContainers
- **Security Testing:** Test authentication flows

### Load Testing
- **Login Endpoint:** Test under concurrent load
- **Rate Limiting:** Test rate limiting behavior
- **Token Refresh:** Test refresh token performance

## Deployment

### Container Configuration
- **Base Image:** mcr.microsoft.com/dotnet/aspnet:8.0-alpine
- **Port:** 5001
- **Dependencies:** .NET 8, Entity Framework Core, PostgreSQL

### Docker Compose Configuration
```yaml
services:
  auth-service:
    build: ./services/auth-service
    ports:
      - "5001:5001"
    environment:
      - ConnectionStrings__DefaultConnection=Server=postgres-auth;Database=AuthDb;Username=postgres;Password=secret;
      - JWT__ISSUER=todo-app
      - JWT__AUDIENCE=todo-app-clients
    depends_on:
      - postgres-auth
```

### Kubernetes Considerations
- **Replicas:** 2 instances for availability
- **Health Checks:** Liveness and readiness probes
- **Secrets:** Database credentials and JWT keys
- **Helm Chart:** Custom Helm chart for deployment

## Monitoring

### Health Checks
- **Endpoint:** GET /health
- **Database Connectivity:** Test database connection
- **External Dependencies:** Check SMTP availability

### Metrics with Prometheus
- `auth_requests_total`: Total authentication requests
- `auth_login_attempts_total`: Login attempts
- `auth_failed_logins_total`: Failed login attempts
- `jwt_tokens_issued_total`: Tokens issued
- `auth_endpoint_duration_seconds`: Request duration

### Logging
- **Structured Logging:** Serilog with JSON format
- **Correlation ID:** Propagate across service calls
- **Security Events:** Log authentication events
- **Error Tracking:** Integration with error tracking service

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-05 | Architecture Team | Initial LLD for Authentication Service |
