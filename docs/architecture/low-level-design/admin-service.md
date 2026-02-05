# Low-Level Design (LLD) - Admin Service

**Version:** 1.0  
**Date:** February 5, 2026  
**Status:** Draft

## Overview

The Admin Service provides administrative operations for the Todo Microservices Solution. It's built with Spring Boot and handles user management, system metrics, and administrative workflows requiring elevated privileges.

## Directory Structure

```
services/admin-service/
├── src/main/java/com/todos/admin/
│   ├── AdminApplication.java
│   ├── config/
│   │   ├── SecurityConfig.java
│   │   └── AdminProperties.java
│   ├── controller/
│   │   ├── UserController.java
│   │   ├── SystemController.java
│   │   └── AdminController.java
│   ├── service/
│   │   ├── UserService.java
│   │   ├── SystemMetricsService.java
│   │   └── AuditService.java
│   ├── repository/
│   │   ├── UserRepository.java
│   │   ├── AuditLogRepository.java
│   │   └── MetricsRepository.java
│   ├── model/
│   │   ├── User.java
│   │   ├── AuditLog.java
│   │   ├── SystemMetrics.java
│   │   └── dto/
│   ├── security/
│   │   ├── JwtFilter.java
│   │   └── AdminRoleValidator.java
│   └── exception/
│       ├── AdminException.java
│       └── GlobalExceptionHandler.java
├── src/main/resources/
│   ├── application.yml
│   └── schema.sql
├── src/test/
├── Dockerfile
└── pom.xml
```

## Core Models

### User (Admin View)
- **Fields:** Id, Email, Status, CreatedAt, LastLoginAt, Role
- **Constraints:** Email unique, Status enum (ACTIVE, SUSPENDED, PENDING)
- **Purpose:** Admin view of user accounts with system status

### AuditLog
- **Fields:** Id, UserId, Action, Entity, EntityId, AdminId, Timestamp, Details
- **Constraints:** None
- **Purpose:** Track all administrative actions performed by admins

### SystemMetrics
- **Fields:** Id, MetricName, Value, Timestamp, Source
- **Constraints:** None
- **Purpose:** Aggregate system health and usage metrics

## Key Endpoints

| Method | Endpoint | Description | Access | Request Body | Response Body |
|--------|----------|-------------|---------|--------------|---------------|
| GET | /api/v1/admin/users | List all users | ADMIN | Query params | List[UserDto] |
| GET | /api/v1/admin/users/{id} | Get user details | ADMIN | None | UserDetailDto |
| PUT | /api/v1/admin/users/{id}/status | Update user status | ADMIN | StatusUpdate | UserDetailDto |
| DELETE | /api/v1/admin/users/{id} | Delete user | ADMIN | None | SuccessResponse |
| GET | /api/v1/admin/audit | Audit log | ADMIN | Query params | List[AuditLogDto] |
| GET | /api/v1/admin/metrics | System metrics | ADMIN | Query params | List[SystemMetricsDto] |
| POST | /api/v1/admin/users/{id}/approve | Approve user | ADMIN | None | SuccessResponse |
| POST | /api/v1/admin/users/{id}/suspend | Suspend user | ADMIN | None | SuccessResponse |
| POST | /api/v1/admin/users/{id}/reactivate | Reactivate user | ADMIN | None | SuccessResponse |

### Request/Response Schemas

**StatusUpdate**
```json
{
  "status": "ACTIVE|SUSPENDED|PENDING"
}
```

**UserDetailDto**
```json
{
  "id": "uuid",
  "email": "string",
  "status": "ACTIVE|SUSPENDED|PENDING",
  "role": "USER|ADMIN",
  "createdAt": "ISO-8601",
  "lastLoginAt": "ISO-8601",
  "todoCount": 0
}
```

**AuditLogDto**
```json
{
  "id": "uuid",
  "action": "string",
  "entity": "USER|TODO|TAG",
  "entityId": "uuid",
  "adminEmail": "string",
  "timestamp": "ISO-8601",
  "details": {}
}
```

## Services

### UserService (Admin)
- **Responsibilities:**
  - Manage user accounts from admin perspective
  - Handle user status changes
  - Provide user statistics
- **Key Methods:**
  - `getAllUsers(pageable)`: Page<UserDetailDto>
  - `getUserById(id)`: UserDetailDto
  - `updateUserStatus(id, status)`: UserDetailDto
  - `deleteUser(id)`: void
  - `approveUser(id)`: void
  - `suspendUser(id)`: void

### SystemMetricsService
- **Responsibilities:**
  - Collect aggregate system metrics
  - Provide system health information
  - Generate usage reports
- **Key Methods:**
  - `getActiveUsers()`: Long
  - `getTotalTodos()`: Long
  - `getSystemHealth()`: SystemHealthDto
  - `getUsageReport(period)`: UsageReportDto

### AuditService
- **Responsibilities:**
  - Track all administrative actions
  - Provide audit log functionality
  - Ensure compliance requirements
- **Key Methods:**
  - `logAction(userId, action, entity, entityId, details)`: void
  - `getAuditLogs(pageable)`: Page<AuditLogDto>
  - `generateComplianceReport()`: ComplianceReportDto

## Configuration

### Key Settings (application.yml)
```yaml
spring:
  application:
    name: admin-service
  datasource:
    url: jdbc:postgresql://postgres-admin:5432/admin_db
    username: postgres
    password: secret
  jpa:
    hibernate:
      ddl-auto: validate
    show-sql: false
  rabbitmq:
    host: rabbitmq
    port: 5672
    username: guest
    password: guest
    virtual-host: /
  
admin:
  auth:
    jwt:
      jwks-uri: http://auth-service:5001/api/v1/auth/jwks
  metrics:
    cache-ttl: 300s
  audit:
    retention-period: 90d
```

### Environment Variables
- **SPRING_DATASOURCE_URL:** PostgreSQL connection string
- **ADMIN_AUTH_JWT_JWKS_URI:** Auth service JWKS endpoint URL
- **SPRING_RABBITMQ_*:** RabbitMQ connection parameters

## Security

### Authentication
- **JWT Validation:** Validate tokens using Auth Service JWKS endpoint
- **Admin Role:** Requires ADMIN role in JWT claims
- **Approved Status:** User must be approved to access admin endpoints

### Authorization
- **ADMIN Only:** All endpoints restricted to ADMIN users
- **Resource Access:** Admins can access all user resources
- **Audit Logging:** All actions logged for compliance

### Data Protection
- **Sensitive Data:** User emails masked in some responses
- **Audit Logs:** Immutable records of all actions
- **Session Management:** Short-lived JWT tokens

## Error Handling

### Standard Error Response
```json
{
  "timestamp": "ISO-8601",
  "status": 400,
  "error": "Bad Request",
  "message": "Error message",
  "path": "/api/v1/admin/users",
  "traceId": "uuid"
}
```

### Custom Exceptions
- **AdminException:** Base admin service exception
- **UserNotFoundException:** User not found
- **InsufficientPrivilegesException:** User lacks required permissions
- **AuditException:** Audit logging failure

### Exception Handling
- **@ControllerAdvice:** Global exception handler
- **@ExceptionHandler:** Specific exception handlers
- **Audit Logging:** All exceptions logged for security

## Testing

### Unit Tests
- **UserService:** Test user management operations
- **AuditService:** Test audit logging functionality
- **SystemMetricsService:** Test metrics aggregation

### Integration Tests
- **Endpoint Testing:** Test all admin endpoints
- **Security Testing:** Test JWT validation and role checks
- **Database Testing:** Test data persistence and queries

### Mock Objects
- **Auth Service:** Mock JWT validation
- **RabbitMQ:** Mock message publishing
- **Database:** Testcontainers for integration tests

## Deployment

### Container Configuration
- **Base Image:** eclipse-temurin:17-jre-alpine
- **Port:** 8003
- **Dependencies:** Spring Boot, Spring Data JPA, MapStruct, Lombok

### Docker Compose Configuration
```yaml
services:
  admin-service:
    build: ./services/admin-service
    ports:
      - "8003:8003"
    environment:
      - SPRING_DATASOURCE_URL=jdbc:postgresql://postgres-admin:5432/admin_db
      - ADMIN_AUTH_JWT_JWKS_URI=http://auth-service:5001/api/v1/auth/jwks
      - SPRING_RABBITMQ_HOST=rabbitmq
    depends_on:
      - postgres-admin
      - rabbitmq
```

### Kubernetes Considerations
- **Replicas:** 2 instances for redundancy
- **RBAC:** Service account with appropriate permissions
- **Secrets:** Database credentials and admin keys
- **Liveness/Readiness:** Custom health checks

## Monitoring

### Spring Boot Actuator
- **Health:** GET /actuator/health
- **Metrics:** GET /actuator/metrics
- **Info:** GET /actuator/info
- **Prometheus:** Metrics exposed for collection

### Admin-Specific Metrics
- `admin_audit_events_total`: Total audit events
- `admin_operations_total`: Admin operations count
- `user_management_duration_ms`: User operation times
- `system_metrics_refresh_count`: Metric refresh attempts

### Database Monitoring
- **Query Performance:** Monitor slow queries
- **Connection Pool:** Track active/idle connections
- **Audit Log Growth:** Monitor table sizes

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-05 | Architecture Team | Initial LLD for Admin Service |
