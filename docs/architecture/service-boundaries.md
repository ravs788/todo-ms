# Service Boundaries - Todo Microservices Solution

**Version:** 1.0  
**Date:** February 5, 2026  
**Status:** Approved

This document defines the bounded contexts, ownership, public APIs, dependencies, and interaction rules for each microservice. It complements ADR-001, HLD, and LLD.

## Contents
 
1. [Principles](#principles)
2. [Authentication Service](#authentication-service-c-net-8)
3. [User Management Service](#user-management-service-c-net-8)
4. [Todo Service](#todo-service-java-spring-boot)
5. [Tag Service](#tag-service-python-fastapi)
6. [Notification Service](#notification-service-python-fastapi--celery)
7. [Admin Service](#admin-service-java-spring-boot)
8. [API Gateway](#api-gateway-kong-boundary)
9. [Cross-Service Interaction Rules](#cross-service-interaction-rules)
10. [Versioning and Compatibility](#versioning-and-compatibility)
11. [Error Model and Idempotency](#error-model-and-idempotency)
12. [Document History](#document-history)

---

## Principles

- **Strong ownership:** Each service owns its data and domain logic
- **No shared databases:** Cross-service access to another service's DB is forbidden
- **Public API contracts:** OpenAPI per service under /v3/api-docs or /openapi.json
- **Backward compatibility:** Non-breaking changes preferred; version endpoints when required
- **Security-first:** All service APIs require JWT; Admin endpoints require ADMIN role

---

## Authentication Service (C# .NET 8)

### Domain/Ownership
- Authentication flows (login, register, refresh/logout, password reset)
- Token issuance (JWT RS256) and JWKS publication
- Refresh tokens and password reset logs
- Does not manage user profile details

### Public API (base: /api/v1/auth)

| Method | Endpoint | Request | Response |
|--------|----------|---------|----------|
| POST | /login | `{ username, password }` | `{ accessToken, refreshToken, expiresIn }` |
| POST | /register | `{ username, email, password }` | `{ userId, status: "PENDING" }` |
| POST | /refresh | `{ refreshToken }` | `{ accessToken, refreshToken, expiresIn }` |
| POST | /logout | `{ refreshToken }` | 204 |
| POST | /forgot-password | `{ username, newPassword }` | 204 |
| GET | /jwks | None | JWKS JSON |

### Dependencies
- PostgreSQL (auth_db)
- Optional Redis (refresh token blacklist)

### Consumes/Publishes Events
- None (synchronous domain)

### AuthZ Rules
- **Anonymous:** /login, /register, /jwks, /forgot-password
- **Authenticated:** /refresh, /logout

---

## User Management Service (C# .NET 8)

### Domain/Ownership
- User profile and preferences
- User status (PENDING/ACTIVE/REJECTED)
- Read model for users (not auth credentials)

### Public API (base: /api/v1/users)

| Method | Endpoint | Request | Response |
|--------|----------|---------|----------|
| GET | /me | None | `{ id, username, email, roles, approved, preferences }` |
| PUT | /me | `{ email?, displayName? }` | `{ ...updated profile... }` |
| PUT | /me/preferences | `{ theme?, notificationsEnabled?, language? }` | `{ ...updated prefs... }` |
| GET | / | None | Admin only (paginated): filters (status, role, search) |
| GET | /{id} | None | Admin only: Retrieve user |

### Dependencies
- PostgreSQL (user_db)
- Auth Service JWKS for JWT validation

### Consumes/Publishes Events
- **Publishes:** user.updated
- **Consumes:** (none initially)

### AuthZ Rules
- **USER:** access to /me, /me/preferences
- **ADMIN:** list and get users, can view statuses

---

## Todo Service (Java Spring Boot)

### Domain/Ownership
- Todo lifecycle and business rules
- Ownership enforcement via token sub claim
- History and reorder operations

### Public API (base: /api/v1/todos)

| Method | Endpoint | Request | Response |
|--------|----------|---------|----------|
| GET | / | Query: page, size, search?, tags?, completed? | Page<Todo> |
| POST | / | `{ title, description?, priority?, dueDate?, reminderDate? }` | Todo |
| GET | /{id} | None | Todo |
| PUT | /{id} | `{ title?, description?, priority?, completed?, dueDate?, reminderDate? }` | Todo |
| DELETE | /{id} | None | 204 |
| PUT | /reorder | `{ orderedIds: UUID[] }` | 204 |
| GET | /history | None | List of change events for current user |

### Dependencies
- PostgreSQL (todo_db)
- RabbitMQ (event publishing)
- Auth Service JWKS

### Consumes/Publishes Events
- **Publishes:** todo.created, todo.updated, todo.completed, todo.deleted
- **Consumes:** (none)

### AuthZ Rules
- **USER (approved == true) required**
- Resource access restricted to token.sub owner unless ADMIN

---

## Tag Service (Python FastAPI)

### Domain/Ownership
- Tag catalog per user
- Tag statistics (popular, counts)
- Tag-to-todo associations (logical link, not owning todos)

### Public API (base: /api/v1/tags)

| Method | Endpoint | Request | Response |
|--------|----------|---------|----------|
| GET | / | None | Tag[] |
| POST | / | `{ name, color? }` | Tag |
| PUT | /{id} | `{ name?, color? }` | Tag |
| DELETE | /{id} | None | 204 |
| GET | /popular | None | `[{ name, count }]` |
| POST | /{id}/todos/{todoId} | None | 204 (associates tag with a todo) |
| DELETE | /{id}/todos/{todoId} | None | 204 (removes association) |

### Dependencies
- PostgreSQL (tag_db)
- Auth Service JWKS
- Todo Service as an external reference (logical only; no direct DB link)

### Consumes/Publishes Events
- **Consumes (optional future):** todo.deleted (to cleanup orphan associations)
- **Publishes:** tag.associated, tag.disassociated (optional future)

### AuthZ Rules
- **USER (approved == true)**
- Only manage tags owned by token.sub

---

## Notification Service (Python FastAPI + Celery)

### Domain/Ownership
- Push subscriptions and notification history
- Scheduling reminders and sending push notifications

### Public API (base: /api/v1/notifications)

| Method | Endpoint | Request | Response |
|--------|----------|---------|----------|
| POST | /subscribe | `{ endpoint, p256dh, auth }` | 204 |
| POST | /unsubscribe | `{ endpoint }` | 204 |
| GET | /history | None | NotificationLog[] |

### Dependencies
- PostgreSQL (notification_db)
- Redis (Celery broker & results)
- RabbitMQ (event consumption)
- VAPID keys

### Consumes/Publishes Events
- **Consumes:** todo.created (with reminderDate), user.approved
- **Publishes:** notification.sent (optional for analytics)

### AuthZ Rules
- **USER (approved == true)**
- Only access own subscriptions and history

---

## Admin Service (Java Spring Boot)

### Domain/Ownership
- Admin operations and audit logging
- Approval workflow that changes user status

### Public API (base: /api/v1/admin)

| Method | Endpoint | Request | Response |
|--------|----------|---------|----------|
| GET | /users/pending | None | User[] (status = PENDING) |
| POST | /users/{id}/approve | None | 204 (changes status, triggers events) |
| POST | /users/{id}/reject | None | 204 (changes status) |
| GET | /stats | None | System metrics snapshot |
| GET | /audit | None | AdminAction[] |

### Dependencies
- PostgreSQL (admin_db)
- RabbitMQ (publish events)
- Auth Service JWKS

### Consumes/Publishes Events
- **Publishes:** user.approved, user.rejected
- **Consumes:** (none)

### AuthZ Rules
- **ADMIN role required for all endpoints**

---

## API Gateway (Kong) Boundary

### Domain/Ownership
- Routing, CORS, rate limiting, logging, optional auth offload in future
- Does not implement business logic

### Public Routes
- /api/v1/auth/* → auth-service
- /api/v1/users/* → user-service
- /api/v1/todos/* → todo-service
- /api/v1/tags/* → tag-service
- /api/v1/notifications/* → notification-service
- /api/v1/admin/* → admin-service

### Non-goals
- No orchestration/business logic
- No data transformation beyond headers

---

## Cross-Service Interaction Rules

### Identity
- **sub claim** is the canonical, immutable user identifier (UUID)
- username/email are informative, not keys

### Authorization
- Every service validates JWT locally via JWKS (issuer = Auth Service)
- Enforce approved == true for USER flows
- ADMIN role required for admin endpoints

### Ownership
- Todo, Tag, Notification entries include ownerId = token.sub
- Services must never rely on usernames for authorization

### Associations
- Tag Service references todos by ID but does not validate existence synchronously
- Best-effort validations via events or on-demand checks (optional)
- Eventual consistency accepted for association integrity

### Events
- **Topic exchange:** todo.events
- **Naming:** resource.action (e.g., user.approved, todo.created)
- **Payload includes:** eventId, eventType, timestamp, and domain fields

### Idempotency
- POST create endpoints support Idempotency-Key header
- Services store key → result mapping for limited TTL to prevent duplicates

---

## Versioning and Compatibility

### Path Versioning
- /api/v1/...

### Non-breaking Changes
- Add fields (response)
- Add optional request fields
- Add endpoints

### Breaking Changes
- Remove fields/endpoints
- Change types or required fields → bump to /api/v2

### Deprecation Policy
- Announce deprecation
- Maintain overlap window
- Telemetry on usage

---

## Error Model and Idempotency

### Error Envelope (All Services)
```json
{
  "traceId": "uuid-or-correlation",
  "timestamp": "ISO-8601",
  "status": 400,
  "error": "Bad Request",
  "code": "VALIDATION_ERROR",
  "message": "Human-readable detail",
  "details": [{ "field": "title", "issue": "must not be blank" }]
}
```

### HTTP Status Mapping
| Status | Description |
|--------|-------------|
| 200/201/204 | Success |
| 400 | Validation errors |
| 401 | Missing/invalid token |
| 403 | Not approved or insufficient role |
| 404 | Resource not found or not owned |
| 409 | Conflicts (unique constraints, duplicate idempotency key with different body) |
| 429 | Rate limited by gateway |
| 500 | Unhandled server errors |

### Idempotency
- **Header:** Idempotency-Key (UUID)
- **Scope:** POST create, subscribe/unsubscribe
- **Behavior:**
  - Same key + same body → return same 201/204 with same response
  - Same key + different body → 409 conflict

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-05 | Architecture Team | Initial boundaries defined |
