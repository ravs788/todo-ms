# Low-Level Design (LLD) - User Management Service

**Version:** 1.0  
**Date:** March 2, 2026  
**Status:** Draft

## Overview

The User Management Service handles user profile CRUD operations, preferences management, and administrative user listing functionality. It's built with ASP.NET Core 8 and follows the same architectural patterns as the Authentication Service.

## Directory Structure

```
services/user-service/
├── UserService.API/
│   ├── Controllers/
│   │   └── UserController.cs
│   ├── Filters/
│   │   └── CorrelationIdFilter.cs
│   ├── Middleware/
│   │   ├── ExceptionHandlingMiddleware.cs
│   │   └── CorrelationIdMiddleware.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
├── UserService.Core/
│   ├── DTOs/
│   │   ├── UserProfileRequest.cs
│   │   ├── UserProfileResponse.cs
│   │   ├── UserPreferencesRequest.cs
│   │   └── UserPreferencesResponse.cs
│   ├── Entities/
│   │   ├── User.cs
│   │   └── UserPreferences.cs
│   ├── Interfaces/
│   │   ├── IUserService.cs
│   │   ├── IUserRepository.cs
│   │   └── IUserPreferencesRepository.cs
│   └── Services/
│       ├── UserService.cs
│       └── UserPreferencesService.cs
├── UserService.Infrastructure/
│   ├── Data/
│   │   └── UserDbContext.cs
│   ├── Migrations/
│   ├── Repositories/
│   │   ├── UserRepository.cs
│   │   └── UserPreferencesRepository.cs
│   └── Security/
│       └── JwtAuthValidator.cs
├── UserService.Tests/
│   ├── Unit/
│   └── Integration/
└── Dockerfile
```

## Core Models

### User
- **Fields:** Id, Username, Email, Status (PENDING, ACTIVE, SUSPENDED), Roles[], Approved, CreatedAt, UpdatedAt
- **Relationship:** One-to-One with UserPreferences
- **Constraints:** Username and Email must be unique

### UserPreferences
- **Fields:** Id, UserId, Theme (light/dark), NotificationsEnabled, Language
- **Relationship:** Many-to-One with User
- **Constraints:** UserId must be unique

## Key Endpoints

| Method | Endpoint | Description | Access | Request Body | Response Body |
|--------|----------|-------------|---------|--------------|---------------|
| GET | /api/v1/users/me | Get current user profile | USER | None | UserProfileResponse |
| PUT | /api/v1/users/me | Update current user profile | USER | UserProfileRequest | UserProfileResponse |
| GET | /api/v1/users/{id} | Get user by ID | ADMIN | None | UserProfileResponse |
| GET | /api/v1/users | List users (paginated) | ADMIN | Query params | PagedResult<UserProfileResponse> |
| PUT | /api/v1/users/{id}/preferences | Update user preferences | ADMIN | UserPreferencesRequest | UserPreferencesResponse |

### Request/Response Schemas

**UserProfileRequest**
```json
{
  "username": "string",
  "email": "string"
}
```

**UserProfileResponse**
```json
{
  "id": "uuid",
  "username": "string",
  "email": "string",
  "status": "string",
  "roles": ["string"],
  "approved": true,
  "createdAt": "ISO-8601",
  "updatedAt": "ISO-8601",
  "preferences": {
    "theme": "string",
    "notificationsEnabled": true,
    "language": "string"
  }
}
```

**PagedResult**
```json
{
  "items": ["UserProfileResponse"],
  "totalCount": 100,
  "page": 1,
  "size": 10,
  "totalPages": 10
}
```

## Services

### UserService
- **Responsibilities:**
  - Manage user profile CRUD operations
  - Handle administrative user listing
  - Enforce ownership and authorization
- **Key Methods:**
  - `GetCurrentUserProfileAsync()`: UserProfileResponse
  - `UpdateCurrentUserProfileAsync(request)`: UserProfileResponse
  - `GetUserByIdAsync(id)`: UserProfileResponse
  - `GetUsersAsync(page, size, search)`: PagedResult<UserProfileResponse>

### UserPreferencesService
- **Responsibilities:**
  - Manage user preferences
  - Create default preferences if none exist
- **Key Methods:**
  - `GetUserPreferencesAsync(userId)`: UserPreferencesResponse
  - `UpdateUserPreferencesAsync(userId, request)`: UserPreferencesResponse

## Security

### JWT Authentication
- **Validation:** Validate JWT tokens using Auth Service JWKS endpoint
- **Claims:** Extract user ID, roles, and approved status
- **Authorization:** Role-based access control (USER, ADMIN)

### Authorization Policies
- **User Policy:** Requires USER or ADMIN role
- **Admin Policy:** Requires ADMIN role
- **Ownership:** Users can only access their own profile data

### JWT Validation Flow
1. Extract token from Authorization header
2. Validate token using JWKS endpoint
3. Check approved status claim
4. Extract roles and user ID
5. Enforce authorization policies

## Configuration

### Key Settings
- **Jwt:**
  - Issuer: "http://auth-service:5001"
  - Audience: "todo-api"
  - JwksUri: "http://auth-service:5001/api/v1/auth/jwks"
- **ConnectionStrings:**
  - UserConnection: PostgreSQL connection string
- **Logging:** Standard ASP.NET Core logging

### Environment Variables
- **ConnectionStrings__UserConnection:** Database connection string
- **Jwt__JwksUri:** Auth service JWKS endpoint URL

## Error Handling

### Standard Error Response
```json
{
  "traceId": "uuid-or-correlation",
  "timestamp": "ISO-8601",
  "status": 400,
  "error": "Bad Request",
  "code": "VALIDATION_ERROR",
  "message": "Error message"
}
```

### Custom Exceptions
- **UserNotFoundException:** User not found
- **UserNotApprovedException:** User not approved
- **ForbiddenException:** Access forbidden
- **DuplicateUsernameException:** Username already exists
- **DuplicateEmailException:** Email already exists

### Validation Rules
- **Username:** 3-100 characters
- **Email:** Valid email format
- **Theme:** Must be "light" or "dark"
- **Language:** 2-10 characters

## Testing

### Unit Tests
- **UserService:** Test profile CRUD operations
- **UserPreferencesService:** Test preferences management
- **JwtAuthValidator:** Test token validation

### Integration Tests
- **Endpoint Testing:** Test all API endpoints
- **Authorization:** Test role-based access control
- **Validation:** Test input validation

## Deployment

### Container Configuration
- **Base Image:** mcr.microsoft.com/dotnet/aspnet:8.0
- **Port:** 5002
- **Health Checks:** /health endpoint

### Kubernetes Considerations
- **Replicas:** 2-3 instances for availability
- **Resources:** Moderate CPU/memory usage
- **Secrets:** Database credentials stored as Kubernetes Secrets

## Monitoring

### Metrics
- `user_profile_requests_total`: Total profile requests
- `user_preferences_requests_total`: Total preferences requests
- `user_operations_duration_ms`: Operation execution time
- `validation_errors_total`: Validation errors by field

### Logging
- Profile updates
- Preferences changes
- Authorization failures
- Validation errors

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-02 | Architecture Team | Initial LLD for User Service |
