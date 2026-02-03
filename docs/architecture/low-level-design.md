# Low-Level Design (LLD) - Todo Microservices Solution

Version: 1.0
Date: March 2, 2026
Status: Draft

This LLD details concrete implementation structures for each microservice, including directory layout, main components, key classes, configuration, error handling, security, observability, and data access. It aligns with ADR-001 and the High-Level Design.

Contents
- Cross-cutting concerns
- Authentication Service (.NET 8)
- User Management Service (.NET 8)
- Todo Service (Spring Boot)
- Tag Service (FastAPI)
- Notification Service (FastAPI + Celery)
- Admin Service (Spring Boot)
- API Gateway (Kong)
- Configuration and secrets
- Database migrations
- Error handling and API responses
- Observability and tracing
- Security and hardening checklist

-------------------------------------------------------------------------------

Cross-Cutting Concerns

- API style: REST/JSON, versioned under /api/v1
- Auth: JWT (RS256), Authorization: Bearer <token>
- Correlation: X-Correlation-Id header propagated
- Idempotency: Idempotency-Key supported on POST create endpoints
- Pagination: page, size query params; default size=10, max size=100
- Sorting/Filtering: standard query params (e.g., sort=createdAt,desc)
- Error envelope:
  {
    "traceId": "uuid-or-correlation",
    "timestamp": "ISO-8601",
    "status": 400,
    "error": "Bad Request",
    "code": "VALIDATION_ERROR",
    "message": "Title is required",
    "details": [{ "field": "title", "issue": "must not be blank" }]
  }
- Content negotiation: application/json; charset=utf-8
- Time: all timestamps in UTC, stored as timestamptz
- OpenAPI: Each service exposes /swagger or /openapi.json
- Health: GET /health (liveness), GET /ready (readiness)
- Rate limiting: via Kong; services must handle 429 gracefully
- Retries: clients retry only idempotent GET; background workers implement exponential backoff

-------------------------------------------------------------------------------

Authentication Service (.NET 8)

Purpose
- Login, register, refresh/logout, forgot-password
- Issue JWT (RS256) and publish JWKS
- Manage refresh tokens and password reset audit

Directory Structure
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

Core Models (simplified)
- User { Id: Guid, Username, Email, PasswordHash, Approved: bool, Roles: string[], CreatedAt }
- RefreshToken { Id, UserId, Token, ExpiresAt, RevokedAt }
- PasswordResetLog { Id, UserId, ResetAt, IpAddress, UserAgent }

Key Endpoints
- POST /api/v1/auth/register
- POST /api/v1/auth/login
- POST /api/v1/auth/refresh
- POST /api/v1/auth/logout
- POST /api/v1/auth/forgot-password
- GET  /api/v1/auth/jwks

TokenService (snippet)
public class TokenService : ITokenService {
  public Task<TokenResponse> GenerateTokens(User user, string? jti = null);
  public Task<bool> RevokeRefreshToken(Guid userId, string token);
}

JwksService (snippet)
public class JwksService : IJwksService {
  public string GetJwksJson(); // Publishes public RSA keys with kid
}

Configuration (appsettings.json)
{
  "Jwt": {
    "Issuer": "http://auth-service:5001",
    "Audience": "todo-api",
    "AccessTokenLifetimeMinutes": 15,
    "RefreshTokenLifetimeDays": 7,
    "KeyRotationDays": 90
  },
  "ConnectionStrings": {
    "AuthConnection": "Host=postgres-auth;Database=auth_db;Username=auth;Password=secret"
  }
}

Security
- Password hashing: BCrypt (work factor 12+)
- Refresh token rotation on each refresh
- JWKS caching enabled downstream
- Optional Redis blacklist for manual revocation

-------------------------------------------------------------------------------

User Management Service (.NET 8)

Purpose
- User profile CRUD, preferences, admin listing

Structure mirrors Auth service
Key Entities: User, UserPreferences
Endpoints
- GET /api/v1/users/me
- PUT /api/v1/users/me
- GET /api/v1/users/{id} (ADMIN)
- GET /api/v1/users (ADMIN, paginated)
- PUT /api/v1/users/{id}/preferences

AuthN/Z
- Validate JWT via Microsoft.IdentityModel.Tokens against Auth JWKS
- Require approved == true for USER access
- Role check for ADMIN list endpoints

-------------------------------------------------------------------------------

Todo Service (Spring Boot 3.x)

Purpose
- Todo CRUD, reorder, history, publish events

Project Structure
services/todo-service/
├── src/main/java/com/todo/service/
│   ├── controller/
│   │   └── TodoController.java
│   ├── dto/
│   │   ├── TodoRequest.java
│   │   ├── TodoResponse.java
│   │   └── ReorderRequest.java
│   ├── entity/
│   │   └── Todo.java
│   ├── mapper/
│   │   └── TodoMapper.java (MapStruct)
│   ├── repository/
│   │   └── TodoRepository.java
│   ├── security/
│   │   ├── SecurityConfig.java
│   │   └── JwtAuthConverter.java
│   ├── service/
│   │   ├── TodoService.java
│   │   └── EventPublisher.java
│   ├── config/
│   │   ├── RabbitConfig.java
│   │   └── OpenApiConfig.java
│   └── TodoServiceApplication.java
├── src/main/resources/
│   ├── application.yml
│   └── db/migration/ (Flyway)
└── Dockerfile

Entity (simplified)
@Entity
@Table(name = "todos")
public class Todo {
  @Id UUID id;
  @Column(nullable=false) UUID userId;
  @Column(nullable=false, length=500) String title;
  String description;
  boolean completed = false;
  String priority = "MEDIUM";
  Instant dueDate;
  Instant reminderDate;
  Integer sortOrder;
  Instant createdAt = Instant.now();
  Instant updatedAt;
}

Repository
public interface TodoRepository extends JpaRepository<Todo, UUID> {
  Page<Todo> findByUserId(UUID userId, Pageable pageable);
  Optional<Todo> findByIdAndUserId(UUID id, UUID userId);
}

SecurityConfig (JWT Resource Server)
http
  .csrf(AbstractHttpConfigurer::disable)
  .authorizeHttpRequests(a -> a
    .requestMatchers("/health","/ready","/v3/api-docs/**","/swagger-ui/**").permitAll()
    .anyRequest().authenticated()
  )
  .oauth2ResourceServer(oauth2 -> oauth2
    .jwt(jwt -> jwt
      .jwkSetUri("${security.jwt.jwks-uri}")
      .jwtAuthenticationConverter(jwtAuthConverter())
    )
  );

JwtAuthConverter
- Extract sub (UUID), roles (from "roles"), approved (boolean)
- Deny if approved != true

Service
- createTodo(request, userId)
  - Validate title not blank
  - Save entity
  - Publish "todo.created" event if reminderDate set
- updateTodo(id, request, userId)
  - Load by id & userId; update mutable fields
- reorderTodos(listOfIds, userId)
  - Validate ownership; batch update sortOrder

Events (RabbitMQ)
- Exchange: todo.events (topic)
- Routing: todo.created, todo.completed, todo.deleted

application.yml (partial)
security:
  jwt:
    jwks-uri: http://auth-service:5001/api/v1/auth/jwks
spring:
  datasource:
    url: jdbc:postgresql://postgres-todo:5432/todo_db
  jpa:
    hibernate:
      ddl-auto: validate
  flyway:
    enabled: true
    locations: classpath:db/migration

-------------------------------------------------------------------------------

Tag Service (FastAPI)

Purpose
- Tag CRUD and associations

Structure
services/tag-service/
├── app/
│   ├── main.py
│   ├── api/
│   │   └── routes_tags.py
│   ├── core/
│   │   ├── config.py
│   │   ├── security.py
│   │   └── logging.py
│   ├── db/
│   │   ├── session.py
│   │   └── base.py
│   ├── models/
│   │   ├── tag.py
│   │   └── todo_tag.py
│   ├── schemas/
│   │   ├── tag.py
│   │   └── common.py
│   └── deps.py
├── migrations/ (Alembic)
├── tests/
└── Dockerfile

Models
class Tag(Base):
  __tablename__ = "tags"
  id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
  user_id = Column(UUID(as_uuid=True), nullable=False, index=True)
  name = Column(String(100), nullable=False)
  color = Column(String(7), default="#3498db")
  created_at = Column(DateTime(timezone=True), server_default=func.now())
  __table_args__ = (UniqueConstraint("user_id","name"),)

Schemas (Pydantic v2)
class TagCreate(BaseModel): name: str; color: str|None
class TagUpdate(BaseModel): name: str|None; color: str|None

Security Middleware
- Decode JWT using JWKS from auth-service
- Verify approved == true
- Inject user_id into request.state.user_id

Routes (examples)
GET /api/v1/tags -> list user tags
POST /api/v1/tags -> create tag
PUT /api/v1/tags/{id} -> update tag
DELETE /api/v1/tags/{id} -> delete tag

-------------------------------------------------------------------------------

Notification Service (FastAPI + Celery)

Purpose
- Push notifications (WebPush), reminder scheduling
- Consumes events from RabbitMQ (todo.created, user.approved)

Structure
services/notification-service/
├── app/
│   ├── main.py
│   ├── api/routes_notifications.py
│   ├── core/config.py
│   ├── services/push_service.py
│   ├── workers/celery_app.py
│   ├── workers/tasks.py
│   ├── consumers/todo_consumer.py
│   ├── models/push_subscription.py
│   ├── schemas/notification.py
│   └── db/session.py
├── tests/
└── Dockerfile

Celery
- Broker: redis://redis:6379/0
- Task: send_reminder_notification(user_id, todo_id, message)
- Retry policy: exponential backoff, max retries 5

WebPush (PyWebPush)
PushService.send(subscription, message, vapid_private_key, vapid_claims)

API
POST /api/v1/notifications/subscribe
POST /api/v1/notifications/unsubscribe
GET  /api/v1/notifications/history

Consumers
- RabbitMQ queue bound to todo.events
- On todo.created with reminderDate -> schedule Celery task

-------------------------------------------------------------------------------

Admin Service (Spring Boot)

Purpose
- Admin operations for user approvals and audit

Structure (similar to Todo Service)
Packages:
- controller: AdminController
- service: AdminService
- repository: AdminActionRepository
- security: Resource server via JWKS
- config: Rabbit config for publishing user.approved/user.rejected

Endpoints
GET  /api/v1/admin/users/pending
POST /api/v1/admin/users/{id}/approve
POST /api/v1/admin/users/{id}/reject
GET  /api/v1/admin/stats
GET  /api/v1/admin/audit

On approve:
- Publish event user.approved { userId, approvedBy, approvedAt }

-------------------------------------------------------------------------------

API Gateway (Kong)

- Mode: DB-less (declarative)
- kong.yml (snippet):
_format_version: "3.0"
_transform: true
services:
  - name: auth-service
    url: http://auth-service:5001
    routes:
      - name: auth-routes
        paths: ["/api/v1/auth"]
  - name: user-service
    url: http://user-service:5002
    routes:
      - name: user-routes
        paths: ["/api/v1/users"]
  - name: todo-service
    url: http://todo-service:8081
    routes:
      - name: todo-routes
        paths: ["/api/v1/todos"]
  - name: tag-service
    url: http://tag-service:8001
    routes:
      - name: tag-routes
        paths: ["/api/v1/tags"]
  - name: notification-service
    url: http://notification-service:8002
    routes:
      - name: notification-routes
        paths: ["/api/v1/notifications"]
  - name: admin-service
    url: http://admin-service:8082
    routes:
      - name: admin-routes
        paths: ["/api/v1/admin"]
plugins:
  - name: cors
    config:
      origins: ["http://localhost:3000"]
      methods: ["GET","POST","PUT","DELETE","OPTIONS"]
      headers: ["Authorization","Content-Type","Idempotency-Key","X-Correlation-Id"]
      credentials: true
  - name: rate-limiting
    config:
      minute: 1000
      policy: local
  - name: request-transformer
    config:
      add:
        headers:
          - "X-Correlation-Id:$(uuid)"

Note: JWT validation remains at services (resource servers) for simplicity. Kong can add auth later (OIDC plugin) if desired.

-------------------------------------------------------------------------------

Configuration & Secrets

Environment Variables (examples)
- AUTH_SERVICE
  - ConnectionStrings__AuthConnection
  - Jwt__Issuer, Jwt__Audience, Jwt__AccessTokenLifetimeMinutes
  - RSA_PRIVATE_KEY (PEM) or KEY_VAULT_URI (future)
- USER_SERVICE
  - ConnectionStrings__UserConnection
  - Auth__JwksUri
- TODO_SERVICE
  - SPRING_DATASOURCE_URL, SPRING_DATASOURCE_USERNAME, SPRING_DATASOURCE_PASSWORD
  - SECURITY_JWT_JWKS_URI
  - RABBITMQ_HOST, RABBITMQ_PORT
- TAG_SERVICE
  - DATABASE_URL=postgresql+psycopg://user:pass@host:5432/tag_db
  - AUTH_JWKS_URI
- NOTIFICATION_SERVICE
  - DATABASE_URL, REDIS_URL
  - VAPID_PRIVATE_KEY, VAPID_SUBJECT (mailto:you@example.com)
  - RABBITMQ_HOST, RABBITMQ_PORT
- ADMIN_SERVICE
  - Spring datasource, JWKS, RabbitMQ

Secrets storage
- Local: .env files (excluded from VCS)
- K8s: Kubernetes Secrets
- Rotations: keys rotated 90 days (Auth service KeyRotationService)

-------------------------------------------------------------------------------

Database Migrations

- Java services: Flyway migrations under classpath:db/migration
- Python services: Alembic migrations (versions/)
- .NET services: EF Core migrations

Naming conventions
- V1__init.sql, V2__add_index_user_id.sql
- Alembic autogenerate with review

-------------------------------------------------------------------------------

Error Handling & API Responses

Standard exception mapping:

Auth/User (.NET):
- FluentValidation for DTOs → 400 VALIDATION_ERROR
- Unauthorized → 401 UNAUTHORIZED
- Forbidden → 403 FORBIDDEN
- NotFound → 404 NOT_FOUND
- Conflict (duplicate username/email) → 409 CONFLICT
- 500 INTERNAL_SERVER_ERROR default handler logs exception with traceId

Spring Services:
- @ControllerAdvice + @ExceptionHandler to map to standard envelope
- MethodArgumentNotValidException → 400 with field details
- AccessDeniedException → 403
- EntityNotFoundException → 404
- DataIntegrityViolationException → 409

FastAPI:
- HTTPException with detail; add middleware to wrap into standard envelope
- Pydantic validation auto 422; convert to 400 with details for consistency

Response examples
POST /api/v1/todos
201 Created
{
  "id": "uuid",
  "title": "Buy groceries",
  "completed": false,
  "priority": "MEDIUM",
  "createdAt": "2026-03-02T10:00:00Z"
}

404 example
{
  "traceId": "abc-123",
  "timestamp": "2026-03-02T10:05:00Z",
  "status": 404,
  "error": "Not Found",
  "code": "RESOURCE_NOT_FOUND",
  "message": "Todo not found"
}

-------------------------------------------------------------------------------

Observability & Tracing

- OpenTelemetry SDK in all services
- Propagate headers: traceparent, tracestate, X-Correlation-Id
- Exporters:
  - .NET: OpenTelemetry.Exporter.Jaeger
  - Spring: opentelemetry-java-instrumentation (agent) or micrometer tracing
  - FastAPI: opentelemetry-instrumentation-fastapi + OTLP to collector
- Metrics:
  - HTTP server duration histogram
  - DB query duration
  - RabbitMQ publish/consume counters
  - Custom: todos_created_total, notifications_sent_total

Log format (JSON)
{ "ts":"...", "lvl":"INFO", "svc":"todo-service", "traceId":"...", "spanId":"...", "msg":"Created todo", "userId":"..." }

-------------------------------------------------------------------------------

Security & Hardening Checklist

- Input validation on all DTOs
- Parameterized queries only (ORMs by default)
- Enforce approved == true on resource access
- Limit page size; prevent heavy queries
- RBAC: ADMIN-only endpoints guarded
- Hide stack traces from clients
- CORS limited to known origins
- TLS termination at ingress (Kong)
- Secrets never logged
- Health endpoints expose minimal data
- Rate limiting at gateway; 429 handling
- Idempotency-Key support for POST creates
- CSRF not applicable to APIs (bearer tokens), but do not accept cookies as auth

-------------------------------------------------------------------------------

Sample DTO Schemas (OpenAPI fragments)

TodoRequest
type: object
required: [title]
properties:
  title: { type: string, minLength: 1, maxLength: 500 }
  description: { type: string, maxLength: 4000 }
  priority: { type: string, enum: [LOW, MEDIUM, HIGH] }
  dueDate: { type: string, format: date-time }
  reminderDate: { type: string, format: date-time }

TagCreate
type: object
required: [name]
properties:
  name: { type: string, minLength: 1, maxLength: 100 }
  color: { type: string, pattern: "^#([A-Fa-f0-9]{6})$" }

-------------------------------------------------------------------------------

Deployment Health Probes

- Liveness: GET /health returns { "status": "healthy" }
- Readiness: GET /ready checks DB, MQ (where applicable)
- Graceful shutdown:
  - .NET IHostApplicationLifetime stopping
  - Spring: management.endpoints.web.exposure.include=health,info
  - FastAPI: lifespan handlers

-------------------------------------------------------------------------------

Future Extensions

- Move Kong JWT/OIDC validation to gateway when feasible
- Add Analytics service (Python/Go) consuming events
- Introduce service mesh for mTLS and traffic shaping (Istio)
- CQRS and read model for complex queries

-------------------------------------------------------------------------------

Document History
- 1.0 (2026-03-02): Initial LLD drafted
