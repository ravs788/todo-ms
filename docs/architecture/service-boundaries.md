# Service Boundaries - Todo Microservices Solution

Version: 1.0
Date: March 2, 2026
Status: Approved

This document defines the bounded contexts, ownership, public APIs, dependencies, and interaction rules for each microservice. It complements ADR-001, HLD, and LLD.

Contents
- Principles
- Authentication Service (C# .NET 8)
- User Management Service (C# .NET 8)
- Todo Service (Java Spring Boot)
- Tag Service (Python FastAPI)
- Notification Service (Python FastAPI + Celery)
- Admin Service (Java Spring Boot)
- API Gateway (Kong) boundary
- Cross-service interaction rules
- Versioning and compatibility
- Error model and idempotency

-------------------------------------------------------------------------------

Principles

- Strong ownership: Each service owns its data and domain logic.
- No shared databases: Cross-service access to another service’s DB is forbidden.
- Public API contracts: OpenAPI per service under /v3/api-docs or /openapi.json.
- Backward compatibility: Non-breaking changes preferred; version endpoints when required.
- Security-first: All service APIs require JWT; Admin endpoints require ADMIN role.

-------------------------------------------------------------------------------

Authentication Service

Domain/Ownership
- Authentication flows (login, register, refresh/logout, password reset)
- Token issuance (JWT RS256) and JWKS publication
- Refresh tokens and password reset logs
- Does not manage user profile details

Public API (base: /api/v1/auth)
- POST /login
  - Request: { username, password }
  - Response: { accessToken, refreshToken, expiresIn }
- POST /register
  - Request: { username, email, password }
  - Response: { userId, status: "PENDING" }
- POST /refresh
  - Request: { refreshToken }
  - Response: { accessToken, refreshToken, expiresIn }
- POST /logout
  - Request: { refreshToken }
  - Response: 204
- POST /forgot-password
  - Request: { username, newPassword }
  - Response: 204
- GET /jwks
  - Response: JWKS JSON

Dependencies
- PostgreSQL (auth_db)
- Optional Redis (refresh token blacklist)

Consumes/Publishes Events
- None (synchronous domain)

AuthZ Rules
- Anonymous: /login, /register, /jwks, /forgot-password
- Authenticated: /refresh, /logout

-------------------------------------------------------------------------------

User Management Service

Domain/Ownership
- User profile and preferences
- User status (PENDING/ACTIVE/REJECTED)
- Read model for users (not auth credentials)

Public API (base: /api/v1/users)
- GET /me
  - Response: { id, username, email, roles, approved, preferences }
- PUT /me
  - Request: { email?, displayName? }
  - Response: { ...updated profile... }
- PUT /me/preferences
  - Request: { theme?, notificationsEnabled?, language? }
  - Response: { ...updated prefs... }
- GET /
  - Admin only (paginated): filters (status, role, search)
- GET /{id}
  - Admin only: Retrieve user

Dependencies
- PostgreSQL (user_db)
- Auth Service JWKS for JWT validation

Consumes/Publishes Events
- Publishes: user.updated
- Consumes: (none initially)

AuthZ Rules
- USER: access to /me, /me/preferences
- ADMIN: list and get users, can view statuses

-------------------------------------------------------------------------------

Todo Service

Domain/Ownership
- Todo lifecycle and business rules
- Ownership enforcement via token sub claim
- History and reorder operations

Public API (base: /api/v1/todos)
- GET /
  - Query: page, size, search?, tags?, completed?
  - Response: Page<Todo>
- POST /
  - Request: { title, description?, priority?, dueDate?, reminderDate? }
  - Response: Todo
- GET /{id}
  - Response: Todo
- PUT /{id}
  - Request: { title?, description?, priority?, completed?, dueDate?, reminderDate? }
  - Response: Todo
- DELETE /{id}
  - Response: 204
- PUT /reorder
  - Request: { orderedIds: UUID[] }
  - Response: 204
- GET /history
  - Response: list of change events for current user

Dependencies
- PostgreSQL (todo_db)
- RabbitMQ (event publishing)
- Auth Service JWKS

Consumes/Publishes Events
- Publishes: todo.created, todo.updated, todo.completed, todo.deleted
- Consumes: (none)

AuthZ Rules
- USER (approved == true) required
- Resource access restricted to token.sub owner unless ADMIN

-------------------------------------------------------------------------------

Tag Service

Domain/Ownership
- Tag catalog per user
- Tag statistics (popular, counts)
- Tag-to-todo associations (logical link, not owning todos)

Public API (base: /api/v1/tags)
- GET /
  - Response: Tag[]
- POST /
  - Request: { name, color? }
  - Response: Tag
- PUT /{id}
  - Request: { name?, color? }
  - Response: Tag
- DELETE /{id}
  - Response: 204
- GET /popular
  - Response: [{ name, count }]
- POST /{id}/todos/{todoId}
  - Response: 204 (associates tag with a todo)
- DELETE /{id}/todos/{todoId}
  - Response: 204 (removes association)

Dependencies
- PostgreSQL (tag_db)
- Auth Service JWKS
- Todo Service as an external reference (logical only; no direct DB link)

Consumes/Publishes Events
- Consumes (optional future): todo.deleted (to cleanup orphan associations)
- Publishes: tag.associated, tag.disassociated (optional future)

AuthZ Rules
- USER (approved == true)
- Only manage tags owned by token.sub

-------------------------------------------------------------------------------

Notification Service

Domain/Ownership
- Push subscriptions and notification history
- Scheduling reminders and sending push notifications

Public API (base: /api/v1/notifications)
- POST /subscribe
  - Request: { endpoint, p256dh, auth }
  - Response: 204
- POST /unsubscribe
  - Request: { endpoint }
  - Response: 204
- GET /history
  - Response: NotificationLog[]

Dependencies
- PostgreSQL (notification_db)
- Redis (Celery broker & results)
- RabbitMQ (event consumption)
- VAPID keys

Consumes/Publishes Events
- Consumes: todo.created (with reminderDate), user.approved
- Publishes: notification.sent (optional for analytics)

AuthZ Rules
- USER (approved == true)
- Only access own subscriptions and history

-------------------------------------------------------------------------------

Admin Service

Domain/Ownership
- Admin operations and audit logging
- Approval workflow that changes user status

Public API (base: /api/v1/admin)
- GET /users/pending
  - Response: User[] (status = PENDING)
- POST /users/{id}/approve
  - Response: 204 (changes status, triggers events)
- POST /users/{id}/reject
  - Response: 204 (changes status)
- GET /stats
  - Response: System metrics snapshot
- GET /audit
  - Response: AdminAction[]

Dependencies
- PostgreSQL (admin_db)
- RabbitMQ (publish events)
- Auth Service JWKS

Consumes/Publishes Events
- Publishes: user.approved, user.rejected
- Consumes: (none)

AuthZ Rules
- ADMIN role required for all endpoints

-------------------------------------------------------------------------------

API Gateway Boundary (Kong)

Domain/Ownership
- Routing, CORS, rate limiting, logging, optional auth offload in future
- Does not implement business logic

Public Routes
- /api/v1/auth/* → auth-service
- /api/v1/users/* → user-service
- /api/v1/todos/* → todo-service
- /api/v1/tags/* → tag-service
- /api/v1/notifications/* → notification-service
- /api/v1/admin/* → admin-service

Non-goals
- No orchestration/business logic
- No data transformation beyond headers

-------------------------------------------------------------------------------

Cross-Service Interaction Rules

- Identity
  - sub claim is the canonical, immutable user identifier (UUID)
  - username/email are informative, not keys

- Authorization
  - Every service validates JWT locally via JWKS (issuer = Auth Service)
  - Enforce approved == true for USER flows
  - ADMIN role required for admin endpoints

- Ownership
  - Todo, Tag, Notification entries include ownerId = token.sub
  - Services must never rely on usernames for authorization

- Associations
  - Tag Service references todos by ID but does not validate existence synchronously
  - Best-effort validations via events or on-demand checks (optional)
  - Eventual consistency accepted for association integrity

- Events
  - Topic exchange: todo.events
  - Naming: resource.action (e.g., user.approved, todo.created)
  - Payload includes eventId, eventType, timestamp, and domain fields

- Idempotency
  - POST create endpoints support Idempotency-Key header
  - Services store key -> result mapping for limited TTL to prevent duplicates

-------------------------------------------------------------------------------

Versioning and Compatibility

- Path versioning: /api/v1/...
- Non-breaking changes:
  - Add fields (response), add optional request fields, add endpoints
- Breaking changes:
  - Remove fields/endpoints, change types or required fields → bump to /api/v2
- Deprecation policy:
  - Announce deprecation, maintain overlap window, telemetry on usage

-------------------------------------------------------------------------------

Error Model and Idempotency

Error Envelope (all services)
{
  "traceId": "uuid-or-correlation",
  "timestamp": "ISO-8601",
  "status": 400,
  "error": "Bad Request",
  "code": "VALIDATION_ERROR",
  "message": "Human-readable detail",
  "details": [{ "field": "title", "issue": "must not be blank" }]
}

HTTP Status Mapping
- 200/201/204: success
- 400: validation errors
- 401: missing/invalid token
- 403: not approved or insufficient role
- 404: resource not found or not owned
- 409: conflicts (unique constraints, duplicate idempotency key with different body)
- 429: rate limited by gateway
- 500: unhandled server errors

Idempotency
- Header: Idempotency-Key (UUID)
- Scope: POST create, subscribe/unsubscribe
- Behavior:
  - Same key + same body → return same 201/204 with same response
  - Same key + different body → 409 conflict

-------------------------------------------------------------------------------

Document History
- 1.0 (2026-03-02): Initial boundaries defined
