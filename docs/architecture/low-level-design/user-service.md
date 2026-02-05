# Low-Level Design (LLD) - User Management Service

**Version:** 1.0  
**Date:** March 2, 2026  
**Status:** Draft

## Overview

The User Management Service handles user profiles, preferences, and account management workflows. It's built with .NET 8 and follows clean architecture principles with Entity Framework Core for data access, ensuring separation of concerns and maintainability.

## Directory Structure

```
services/user-service/
├── src/
│   ├── User.Api/                           # ASP.NET Core Web API
│   │   ├── Program.cs
│   │   ├── Controllers/
│   │   │   ├── UserController.cs
│   │   │   └── PreferencesController.cs
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   └── CorrelationMiddleware.cs
│   │   └── Program.cs
│   ├── User.Application/                  # Application Layer
│   │   ├── Services/
│   │   │   ├── IUserService.cs
│   │   │   ├── UserService.cs
│   │   │   ├── PreferencesService.cs
│   │   │   └── ProfileService.cs
│   │   ├── DTOs/
│   │   │   ├── UpdateProfileRequest.cs
│   │   │   ├── UpdatePreferencesRequest.cs
│   │   │   ├── UserResponse.cs
│   │   │   └── ErrorResponse.cs
│   │   └── Interfaces/
│   │       └── IUnitOfWork.cs
│   ├── User.Infrastructure/               # Infrastructure Layer
│   │   ├── Data/
│   │   │   ├── UserDbContext.cs
│   │   │   ├── Migrations/
│   │   │   └── Repositories/
│   │   │       ├── IUserRepository.cs
│   │   │       ├── IPreferencesRepository.cs
│   │   │       ├── UserRepository.cs
│   │   │       └── PreferencesRepository.cs
│   │   └── Services/
│   │       └── IEmailService.cs
│   └── User.Domain/                        # Domain Layer
│       ├── Entities/
│       │   ├── User.cs
│       │   ├── UserProfile.cs
│       │   └── UserPreferences.cs
│       ├── Interfaces/
│       │   ├── IUserRepository.cs
│       │   ├── IPreferencesRepository.cs
│       │   └── IEmailRepository.cs
│       └── ValueObjects/
│           └── Email.cs
│
├── tests/                                  # Test Projects
│   ├── User.Api.Tests/
│   ├── User.Application.Tests/
│   └── User.Integration.Tests/
│
├── Dockerfile
└── README.md
```

## Core Models

### User
- **Fields:** Id, Email, CreatedAt, UpdatedAt
- **Constraints:** Email must be unique
- **Indexes:** Email unique index
- **Relationships:** One-to-One with UserProfile and UserPreferences

### UserProfile
- **Fields:** Id, UserId, FirstName, LastName, AvatarUrl, Bio
- **Constraints:** Foreign key to User, One-to-One relationship
- **Purpose:** Store user profile information

### UserPreferences
- **Fields:** Id, UserId, Theme, Language, TimeZone, NotificationsEnabled
- **Constraints:** Foreign key to User, One-to-One relationship
- **Purpose:** Store user preferences and settings

## Key Endpoints

| Method | Endpoint | Description | Access | Request Body | Response Body |
|--------|----------|-------------|---------|--------------|---------------|
| GET | /api/v1/users/profile | Get user profile | USER | None | UserResponse |
| PUT | /api/v1/users/profile | Update user profile | USER | UpdateProfileRequest | UserResponse |
| GET | /api/v1/users/preferences | Get user preferences | USER | None | UserResponse |
| PUT | /api/v1/users/preferences | Update user preferences | USER | UpdatePreferencesRequest | UserResponse |
| GET | /api/v1/users/{id} | Get user by ID (public profile) | PUBLIC | None | UserResponse |
| PUT | /api/v1/users/{id}/status | Update user status | ADMIN | StatusUpdate | UserResponse |

### Request/Response Schemas

**UpdateProfileRequest**
```json
{
  "firstName": "string",
  "lastName": "string",
  "avatarUrl": "string",
  "bio": "string"
}
```

**UpdatePreferencesRequest**
```json
{
  "theme": "light|dark",
  "language": "en",
  "timeZone": "UTC",
  "notificationsEnabled": true
}
```

**UserResponse**
```json
{
  "id": "uuid",
  "email": "string",
  "profile": {
    "firstName": "string",
    "lastName": "string",
    "avatarUrl": "string",
    "bio": "string"
  },
  "preferences": {
    "theme": "light|dark",
    "language": "en",
    "timeZone": "UTC",
    "notificationsEnabled": true
  },
  "createdAt": "ISO-8601",
  "updatedAt": "ISO-8601"
}
```

## Services

### UserService
- **Responsibilities:**
  - Manage user account operations
  - Handle user profile updates
  - Provide user information to other services
- **Key Methods:**
  - `GetUserProfile(userId)`: UserProfile
  - `UpdateUserProfile(userId, request)`: UserProfile
  - `GetUserPreferences(userId)`: UserPreferences
  - `UpdateUserPreferences(userId, request)`: UserPreferences
  - `GetUserById(id)`: User

### PreferencesService
- **Responsibilities:**
  - Manage user preferences
  - Handle theme settings
  - Manage notification preferences
- **Key Methods:**
  - `GetPreferences(userId)`: UserPreferences
  - `UpdatePreferences(userId, request)`: UserPreferences
  - `GetDefaultPreferences()`: UserPreferences

### ProfileService
- **Responsibilities:**
  - Manage user profile information
  - Handle avatar uploads
  - Validate profile data
- **Key Methods:**
  - `GetProfile(userId)`: UserProfile
  - `UpdateProfile(userId, request)`: UserProfile
  - `ValidateProfile(request)`: ValidationResult

## Configuration

### Key Settings (appsettings.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=UserDb;User Id=postgres;Password=secret;"
  },
  "Storage": {
    "AvatarStorage": "Local",
    "AvatarPath": "/app/avatars",
    "MaxAvatarSize": "5MB"
  },
  "Notifications": {
    "EmailVerification": true,
    "PasswordReset": true
  }
}
```

### Environment Variables
- **ConnectionStrings__DefaultConnection:** PostgreSQL connection string
- **STORAGE__AVATAR_STORAGE:** Avatar storage type (Local/Azure/S3)
- **STORAGE__AVATAR_PATH:** Local path for avatar storage
- **NOTIFICATIONS__EMAIL_VERIFICATION:** Email verification enabled

## Security

### Authentication
- **JWT Validation:** Validate tokens from Auth Service
- **User Ownership:** Users can only access their own profiles
- **Admin Access:** Admins can access any user's profile

### Data Protection
- **PII Handling:** Sensitive data encrypted at rest
- **Avatar Storage:** Secure storage for user avatars
- **Authorization:** Role-based access control

### API Security
- **Rate Limiting:** Limit profile update requests
- **Input Validation:** Validate all profile data
- **Size Limits:** Limit avatar upload sizes

## Error Handling

### Standard Error Response
```json
{
  "timestamp": "2026-03-02T00:00:00.000Z",
  "status": 400,
  "error": "Bad Request",
  "message": "Validation failed",
  "path": "/api/v1/users/profile",
  "traceId": "uuid"
}
```

### Custom Exceptions
- **UserNotFoundException:** User not found
- **ProfileUpdateException:** Profile update failed
- **PreferencesUpdateException:** Preferences update failed
- **AvatarUploadException:** Avatar upload failed
- **UnauthorizedAccessException:** User lacks permissions

### Exception Handling
- **Middleware:** Global exception handling middleware
- **Logging:** All exceptions logged with correlation ID
- **Validation:** FluentValidation for input validation

## Testing

### Unit Tests
- **UserService:** Test user management operations
- **PreferencesService:** Test preference management
- **ProfileService:** Test profile management and validation

### Integration Tests
- **Endpoint Testing:** Test all API endpoints
- **Database Testing:** Test data persistence with TestContainers
- **Security Testing:** Test authentication and authorization

### Load Testing
- **Profile Updates:** Test under concurrent load
- **Avatar Uploads:** Test file upload performance
- **Preference Changes:** Test preference update performance

## Deployment

### Container Configuration
- **Base Image:** mcr.microsoft.com/dotnet/aspnet:8.0-alpine
- **Port:** 8000
- **Dependencies:** .NET 8, Entity Framework Core, PostgreSQL

### Docker Compose Configuration
```yaml
services:
  user-service:
    build: ./services/user-service
    ports:
      - "8000:8000"
    environment:
      - ConnectionStrings__DefaultConnection=Server=postgres-user;Database=UserDb;Username=postgres;Password=secret;
      - STORAGE__AVATAR_STORAGE=Local
      - STORAGE__AVATAR_PATH=/app/avatars
    volumes:
      - ./avatars:/app/avatars
    depends_on:
      - postgres-user
```

### Kubernetes Considerations
- **Replicas:** 2 instances for availability
- **Health Checks:** Liveness and readiness probes
- **Storage:** Persistent volume for avatar storage
- **Secrets:** Database credentials and storage keys

## Monitoring

### Health Checks
- **Endpoint:** GET /health
- **Database Connectivity:** Test database connection
- **Storage Check:** Test avatar storage accessibility

### Metrics with Prometheus
- `user_requests_total`: Total user service requests
- `profile_updates_total`: Profile update count
- `preference_changes_total`: Preference change count
- `avatar_uploads_total`: Avatar upload count
- `user_endpoint_duration_seconds`: Request duration

### Logging
- **Structured Logging:** Serilog with JSON format
- **Correlation ID:** Propagate across service calls
- **Profile Events:** Log profile updates
- **Error Tracking:** Integration with error tracking service

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-02 | Architecture Team | Initial LLD for User Management Service |
