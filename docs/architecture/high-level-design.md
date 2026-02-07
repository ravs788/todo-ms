# High-Level Design (HLD) - Todo Microservices Solution

**Version:** 1.0  
**Date:** February 5, 2026  
**Status:** Approved

---

## Table of Contents

1. [System Overview](#system-overview)
2. [Architecture Principles](#architecture-principles)
3. [System Architecture Diagram](#system-architecture-diagram)
4. [Service Catalog](#service-catalog)
5. [Data Architecture](#data-architecture)
6. [Communication Patterns](#communication-patterns)
7. [Security Architecture](#security-architecture)
8. [Deployment Architecture](#deployment-architecture)
9. [Scalability & Performance](#scalability--performance)
10. [Observability](#observability)
11. [Disaster Recovery](#disaster-recovery)

---

## System Overview

The Todo Microservices Solution is a distributed system that decomposes a monolithic Todo application into 7 independent microservices. The system follows microservices architecture patterns with a polyglot technology stack (C#, Java, Python) and is designed for Docker and Kubernetes deployment.

### Key Characteristics

- **Architecture Style**: Microservices
- **Technology Stack**: Polyglot (C# .NET 8, Java Spring Boot 3.x, Python FastAPI)
- **Communication**: REST/HTTP + Event-Driven (RabbitMQ)
- **Data Storage**: PostgreSQL per service + Redis for caching
- **Deployment**: Docker containers orchestrated by Kubernetes
- **Frontend**: React 18+ SPA (targeting React 19)

---

## Architecture Principles

### 1. Single Responsibility
Each service owns a specific domain and related business logic. Services are small, focused, and independently deployable.

### 2. Database per Service
Each service has its own database to ensure loose coupling and independent scalability. No direct database access between services.

### 3. API Gateway Pattern
All client requests flow through a centralized API Gateway (Kong) which handles routing, authentication, rate limiting, and CORS.

### 4. Event-Driven Communication
Services communicate asynchronously via RabbitMQ for operations that don't require immediate response (e.g., notifications, analytics).

### 5. Observability First
Every service implements distributed tracing, metrics, and structured logging from day one. Local development uses OpenTelemetry SDKs to emit traces to Jaeger and metrics to Prometheus, visualized via Grafana dashboards.

### 6. Fail Fast
Services validate inputs early and return explicit errors. Circuit breakers prevent cascading failures.

### 7. Infrastructure as Code
All infrastructure (Docker Compose, Kubernetes manifests, Helm charts) is version-controlled and automated.

---

## Current Implementation Status (Local)

- Infrastructure
  - Docker Compose stack running: PostgreSQL (per service), Redis, RabbitMQ, Kong (DB-less), Prometheus, Grafana, Jaeger
  - API Gateway: Kong configured with routes for /api/v1/* and a test upstream /httpbin; global plugins enabled (CORS, correlation-id, rate-limiting, prometheus)
  - Observability: Prometheus scraping enabled; Grafana pre-provisioned with Prometheus datasource; Jaeger UI reachable
  - Developer UX: Makefile (up/down/status/ps/logs/smoke), scripts/smoke.sh, .env.example

- Verified
  - make smoke reports 200 for: Kong /httpbin/status/200, Prometheus /-/ready, Grafana /api/health, RabbitMQ UI, Jaeger UI
  - Auth service verified via Kong: 200 for /api/v1/auth/health and /api/v1/auth/jwks; register/login/refresh/logout flows succeed; RS256 JWT with kid; refresh rotation persisted in postgres-auth
  - Port conflict on 8080 mitigated: httpbin mapped to host 18080 (direct checks return 200)

- Next (services)
  - Implement Todo thin slice (JWT validation via JWKS, CRUD) and route via Kong

## System Architecture Diagram

```text
┌───────────────────────────────────────────────────────────────────────┐
│                           CLIENT LAYER                                 │
│                                                                        │
│    ┌─────────────────────────────────────────────────────────┐       │
│    │              React Frontend (SPA)                        │       │
│    │          Port 3000 - Nginx Static Server                │       │
│    │   - Authentication UI  - Todo Management UI             │       │
│    │   - User Profile       - Tag Management                 │       │
│    │   - Notifications      - Admin Panel                    │       │
│    └──────────────────────┬──────────────────────────────────┘       │
└───────────────────────────┼────────────────────────────────────────────┘
                            │
                            │ HTTPS/REST (JWT Bearer Token)
                            │
┌───────────────────────────┼────────────────────────────────────────────┐
│                           ▼          API GATEWAY LAYER                 │
│    ┌────────────────────────────────────────────────────────┐         │
│    │                  Kong API Gateway                       │         │
│    │                    Port 8000                            │         │
│    │                                                          │         │
│    │  Features:                                              │         │
│    │  ✓ Request Routing        ✓ JWT Validation            │         │
│    │  ✓ Rate Limiting          ✓ CORS Handling             │         │
│    │  ✓ Load Balancing         ✓ Request/Response Logging  │         │
│    │  ✓ Circuit Breaking       ✓ API Versioning            │         │
│    └────┬────────┬─────────┬──────────┬──────────┬──────────┘         │
└─────────┼────────┼─────────┼──────────┼──────────┼────────────────────┘
          │        │         │          │          │
    /auth │  /users│  /todos │   /tags  │  /notify │  /admin
          │        │         │          │          │          │
┌─────────┼────────┼─────────┼──────────┼──────────┼──────────┼─────────┐
│         ▼        ▼         ▼          ▼          ▼          ▼         │
│  ┌──────────┐ ┌──────┐ ┌───────┐ ┌────────┐ ┌──────────┐ ┌───────┐  │
│  │   Auth   │ │ User │ │ Todo  │ │  Tag   │ │  Notify  │ │ Admin │  │
│  │ Service  │ │Service│ │Service│ │Service │ │ Service  │ │Service│  │
│  │          │ │      │ │       │ │        │ │          │ │       │  │
│  │   C#     │ │  C#  │ │ Java  │ │ Python │ │  Python  │ │ Java  │  │
│  │ .NET 8   │ │.NET 8│ │Spring │ │FastAPI │ │ FastAPI  │ │Spring │  │
│  │          │ │      │ │ Boot  │ │        │ │          │ │ Boot  │  │
│  │  :5001   │ │:5002 │ │ :8081 │ │ :8001  │ │  :8002   │ │ :8082 │  │
│  │          │ │      │ │       │ │        │ │          │ │       │  │
│  │ • Login  │ │• CRUD│ │• CRUD │ │• CRUD  │ │• Push    │ │• User │  │
│  │ • JWT    │ │• Prof│ │• Undo │ │• Search│ │  Notifs  │ │  Appr │  │
│  │ • JWKS   │ │ile  │ │• Tags │ │• Stats │ │• Remind  │ │oval  │  │
│  │ • Refresh│ │• Pref│ │       │ │        │ │• Sched   │ │• Audit│  │
│  └────┬─────┘ └──┬───┘ └───┬───┘ └───┬────┘ └────┬─────┘ └───┬───┘  │
│       │          │         │         │            │            │      │
│  MICROSERVICES LAYER                                                  │
└───────┼──────────┼─────────┼─────────┼────────────┼────────────┼──────┘
        │          │         │         │            │            │
┌───────┼──────────┼─────────┼─────────┼────────────┼────────────┼──────┐
│       ▼          ▼         ▼         ▼            ▼            ▼      │
│  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐  │
│  │auth_db │ │user_db │ │todo_db │ │ tag_db │ │notif_db│ │admin_db│  │
│  │        │ │        │ │        │ │        │ │        │ │        │  │
│  │Postgres│ │Postgres│ │Postgres│ │Postgres│ │Postgres│ │Postgres│  │
│  │ :5436  │ │ :5432  │ │ :5433  │ │ :5434  │ │ :5435  │ │ :5437  │  │
│  └────────┘ └────────┘ └────────┘ └────────┘ └────────┘ └────────┘  │
│                                                                        │
│  DATA LAYER - Database per Service Pattern                            │
└────────────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────────┐
│                    CROSS-CUTTING CONCERNS                               │
│                                                                         │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────────────┐    │
│  │   RabbitMQ   │    │    Redis     │    │  Observability Stack │    │
│  │  Event Bus   │    │   Caching    │    │                      │    │
│  │   :5672      │    │   :6379      │    │  • Jaeger (:16686)   │    │
│  │              │    │              │    │  • Prometheus (:9090)│    │
│  │ Exchanges:   │    │ Use Cases:   │    │  • Grafana (:3001)   │    │
│  │ • user.evt   │    │ • Sessions   │    │  • ELK (:5601)       │    │
│  │ • todo.evt   │    │ • Rate Limit │    │                      │    │
│  │ • notif.evt  │    │ • JWT Tokens │    │  Correlation IDs     │    │
│  └──────────────┘    └──────────────┘    └──────────────────────┘    │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Service Catalog

### 1. Authentication Service (C# .NET 8)

**Responsibility:** User authentication, JWT token lifecycle management

**Port:** 5001

**Key Endpoints:**
- `POST /api/v1/auth/login` - User login
- `POST /api/v1/auth/register` - New user registration
- `POST /api/v1/auth/refresh` - Refresh access token
- `POST /api/v1/auth/logout` - Token revocation
- `POST /api/v1/auth/forgot-password` - Password reset
- `GET /api/v1/auth/jwks` - Public key endpoint

**Dependencies:**
- PostgreSQL (auth_db)
- Redis (token blacklist)

**Technology Stack:**
- ASP.NET Core 8 Web API
- Entity Framework Core 8
- System.IdentityModel.Tokens.Jwt
- BCrypt.Net (password hashing)

---

### 2. User Management Service (C# .NET 8)

**Responsibility:** User profile and preferences management

**Port:** 5002

**Key Endpoints:**
- `GET /api/v1/users/me` - Current user profile
- `PUT /api/v1/users/me` - Update profile
- `GET /api/v1/users/{id}` - Get user (Admin)
- `GET /api/v1/users` - List users (Admin, paginated)
- `PUT /api/v1/users/{id}/preferences` - Update preferences

**Dependencies:**
- PostgreSQL (user_db)
- Auth Service (JWT validation via JWKS)

**Technology Stack:**
- ASP.NET Core 8 Web API
- Entity Framework Core 8
- AutoMapper

---

### 3. Todo Service (Java Spring Boot 3.x)

**Responsibility:** Todo CRUD operations, business logic

**Port:** 8081

**Key Endpoints:**
- `GET /api/v1/todos` - List todos (filtered, paginated)
- `POST /api/v1/todos` - Create todo
- `GET /api/v1/todos/{id}` - Get todo by ID
- `PUT /api/v1/todos/{id}` - Update todo
- `DELETE /api/v1/todos/{id}` - Delete todo
- `PUT /api/v1/todos/reorder` - Batch reorder
- `POST /api/v1/todos/{id}/complete` - Mark complete
- `GET /api/v1/todos/history` - Undo/redo history

**Dependencies:**
- PostgreSQL (todo_db)
- Tag Service (for tag associations)
- RabbitMQ (publish todo events)

**Technology Stack:**
- Spring Boot 3.2
- Spring Data JPA
- Spring Security (OAuth2 Resource Server)
- Spring Cloud Stream (RabbitMQ)
- MapStruct
- Flyway (migrations)

---

### 4. Tag Service (Python FastAPI)

**Responsibility:** Tag management and todo-tag associations

**Port:** 8001

**Key Endpoints:**
- `GET /api/v1/tags` - List tags for user
- `POST /api/v1/tags` - Create tag
- `PUT /api/v1/tags/{id}` - Update tag
- `DELETE /api/v1/tags/{id}` - Delete tag
- `GET /api/v1/tags/popular` - Popular tags
- `POST /api/v1/tags/{id}/todos/{todoId}` - Associate tag with todo

**Dependencies:**
- PostgreSQL (tag_db)
- Auth Service (JWT validation)

**Technology Stack:**
- Python 3.12
- FastAPI 0.110
- SQLAlchemy 2.0
- Pydantic v2
- Alembic (migrations)

---

### 5. Notification Service (Python FastAPI)

**Responsibility:** Push notifications, reminder scheduling

**Port:** 8002

**Key Endpoints:**
- `POST /api/v1/notifications/subscribe` - Register push subscription
- `POST /api/v1/notifications/unsubscribe` - Remove subscription
- `GET /api/v1/notifications/history` - Notification log
- `POST /api/v1/notifications/test` - Send test notification

**Dependencies:**
- PostgreSQL (notification_db)
- Redis (Celery broker + result backend)
- RabbitMQ (consume todo events)

**Technology Stack:**
- Python 3.12
- FastAPI 0.110
- Celery 5.3
- PyWebPush (VAPID notifications)
- APScheduler (reminder scheduling)
- SQLAlchemy 2.0

---

### 6. Admin Service (Java Spring Boot 3.x)

**Responsibility:** Admin operations, user approval workflow

**Port:** 8082

**Key Endpoints:**
- `GET /api/v1/admin/users/pending` - Pending users
- `POST /api/v1/admin/users/{id}/approve` - Approve user
- `POST /api/v1/admin/users/{id}/reject` - Reject user
- `GET /api/v1/admin/stats` - System statistics
- `GET /api/v1/admin/audit` - Audit log

**Dependencies:**
- PostgreSQL (admin_db)
- User Service (via events)
- RabbitMQ (publish user approval events)

**Technology Stack:**
- Spring Boot 3.2
- Spring Data JPA
- Spring Security
- Spring Cloud Stream

---

### 7. API Gateway (Kong)

**Responsibility:** Request routing, security, rate limiting

**Port:** 8000

**Features:**
- Route requests to appropriate services
- JWT validation (via Auth Service JWKS)
- Rate limiting (per IP, per user)
- CORS handling
- Request/response logging
- Circuit breaker pattern
- API versioning

**Technology:**
- Kong Gateway 3.x
- Kong plugins (rate-limiting, jwt, cors, prometheus)
- Redis (rate limiting storage)

---

## Data Architecture

### Database Schema Strategy

Each service owns its data with dedicated PostgreSQL databases:

#### Auth Service Database (auth_db)

```sql
CREATE TABLE refresh_tokens (
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL,
  token TEXT NOT NULL,
  expires_at TIMESTAMP NOT NULL,
  created_at TIMESTAMP DEFAULT NOW(),
  revoked_at TIMESTAMP
);

CREATE TABLE password_reset_log (
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL,
  reset_at TIMESTAMP DEFAULT NOW(),
  ip_address VARCHAR(45),
  user_agent TEXT
);
```

#### User Service Database (user_db)

```sql
CREATE TABLE users (
  id UUID PRIMARY KEY,
  username VARCHAR(100) UNIQUE NOT NULL,
  email VARCHAR(255) UNIQUE NOT NULL,
  status VARCHAR(20) DEFAULT 'PENDING',
  roles TEXT[] DEFAULT '{"USER"}',
  approved BOOLEAN DEFAULT FALSE,
  created_at TIMESTAMP DEFAULT NOW(),
  updated_at TIMESTAMP
);

CREATE TABLE user_preferences (
  id UUID PRIMARY KEY,
  user_id UUID UNIQUE NOT NULL,
  theme VARCHAR(20) DEFAULT 'light',
  notifications_enabled BOOLEAN DEFAULT TRUE,
  language VARCHAR(10) DEFAULT 'en',
  FOREIGN KEY (user_id) REFERENCES users(id)
);
```

#### Todo Service Database (todo_db)

```sql
CREATE TABLE todos (
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL,
  title VARCHAR(500) NOT NULL,
  description TEXT,
  completed BOOLEAN DEFAULT FALSE,
  priority VARCHAR(20) DEFAULT 'MEDIUM',
  due_date TIMESTAMP,
  reminder_date TIMESTAMP,
  sort_order INTEGER,
  created_at TIMESTAMP DEFAULT NOW(),
  updated_at TIMESTAMP
);

CREATE TABLE todo_history (
  id UUID PRIMARY KEY,
  todo_id UUID NOT NULL,
  user_id UUID NOT NULL,
  action VARCHAR(50) NOT NULL,
  snapshot JSONB NOT NULL,
  created_at TIMESTAMP DEFAULT NOW()
);
```

#### Tag Service Database (tag_db)

```sql
CREATE TABLE tags (
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL,
  name VARCHAR(100) NOT NULL,
  color VARCHAR(7) DEFAULT '#3498db',
  created_at TIMESTAMP DEFAULT NOW(),
  UNIQUE(user_id, name)
);

CREATE TABLE todo_tags (
  todo_id UUID NOT NULL,
  tag_id UUID NOT NULL,
  created_at TIMESTAMP DEFAULT NOW(),
  PRIMARY KEY (todo_id, tag_id)
);
```

#### Notification Service Database (notification_db)

```sql
CREATE TABLE push_subscriptions (
  id UUID PRIMARY KEY,
  user_id UUID UNIQUE NOT NULL,
  endpoint TEXT NOT NULL,
  p256dh TEXT NOT NULL,
  auth TEXT NOT NULL,
  created_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE notification_log (
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL,
  type VARCHAR(50) NOT NULL,
  title VARCHAR(200),
  body TEXT,
  sent_at TIMESTAMP DEFAULT NOW(),
  status VARCHAR(20)
);
```

#### Admin Service Database (admin_db)

```sql
CREATE TABLE admin_actions (
  id UUID PRIMARY KEY,
  admin_user_id UUID NOT NULL,
  target_user_id UUID,
  action VARCHAR(100) NOT NULL,
  details JSONB,
  created_at TIMESTAMP DEFAULT NOW()
);
```

### Redis Usage

Redis serves both as a cache and as infrastructure backing certain components (rate limiting, JWT tokens, Celery).

---

## Communication Patterns

### Synchronous Communication (REST/HTTP)

**Client → Gateway → Services**

```text
Client Request:
GET /api/v1/todos?page=1&size=10
Authorization: Bearer eyJhbGc...

Gateway:
1. Validate JWT via JWKS
2. Extract user_id from token
3. Route to Todo Service

Todo Service:
1. Receive request with validated user context
2. Query database for user's todos
3. Return paginated response
```

### Asynchronous Communication (RabbitMQ)

**Event-Driven Architecture**

```text
Event Flow Example:

1. Admin approves user
   Admin Service publishes:
   {
     "eventType": "user.approved",
     "userId": "uuid",
     "timestamp": "2026-02-05T10:00:00Z"
   }

2. Notification Service consumes event
   → Sends welcome notification to user

3. Analytics Service (future) consumes event
   → Updates user statistics
```

**RabbitMQ Exchange Configuration:**

```text
Exchange: todo.events (topic)
├── Routing Key: user.approved
│   └── Queues: notification-service-queue
├── Routing Key: user.rejected
│   └── Queues: notification-service-queue
├── Routing Key: todo.created
│   └── Queues: notification-service-queue, analytics-queue
└── Routing Key: todo.completed
    └── Queues: analytics-queue
```

This keeps write-paths in Todo and Admin services decoupled from Notification/Analytics consumers while still supporting fan-out on key events.

---

## Security Architecture

### Authentication Flow

```text
1. User Login
   Frontend → Gateway → Auth Service
   
   Auth Service:
   - Validate credentials
   - Generate JWT (RS256)
   - Store refresh token
   - Return tokens
   
   Response:
   {
     "accessToken": "eyJhbGc...",
     "refreshToken": "uuid",
     "expiresIn": 900
   }

2. Authenticated Requests
   Frontend → Gateway (with JWT)
   
   Gateway:
   - Fetch JWKS from Auth Service (cached)
   - Validate JWT signature
   - Extract claims
   - Route to service with user context

3. Token Refresh
   Frontend → Gateway → Auth Service
   
   Auth Service:
   - Validate refresh token
   - Issue new access token
   - Rotate refresh token (optional)
```

### Authorization Matrix

| Endpoint | Anonymous | USER | ADMIN |
|----------|-----------|------|-------|
| POST /auth/register | ✓ | ✓ | ✓ |
| POST /auth/login | ✓ | ✓ | ✓ |
| GET /users/me | ✗ | ✓ | ✓ |
| GET /todos | ✗ | ✓ (own) | ✓ (all) |
| POST /todos | ✗ | ✓ | ✓ |
| PUT /todos/{id} | ✗ | ✓ (own) | ✓ (all) |
| DELETE /todos/{id} | ✗ | ✓ (own) | ✓ (all) |
| GET /admin/users/pending | ✗ | ✗ | ✓ |
| POST /admin/users/{id}/approve | ✗ | ✗ | ✓ |

Future roles (e.g., SUPPORT, AUDITOR) can be added by extending JWT roles and gateway/service policies.

---

## Deployment Architecture

### Docker Compose (Local Development)

```yaml
version: '3.8'

services:
  # Databases
  postgres-auth:
    image: postgres:16-alpine
    ports: ["5436:5432"]
    
  postgres-user:
    image: postgres:16-alpine
    ports: ["5432:5432"]
    
  postgres-todo:
    image: postgres:16-alpine
    ports: ["5433:5432"]
    
  # Services
  auth-service:
    build: ./services/auth-service
    ports: ["5001:5001"]
    depends_on: [postgres-auth, redis]
    
  todo-service:
    build: ./services/todo-service
    ports: ["8081:8081"]
    depends_on: [postgres-todo, rabbitmq]
    
  # Infrastructure
  kong:
    image: kong:3.5
    ports: ["8000:8000", "8001:8001"]
    
  rabbitmq:
    image: rabbitmq:3.12-management-alpine
    ports: ["5672:5672", "15672:15672"]
    
  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]
```

### Kubernetes Architecture

```text
Namespace: todo-app

Deployments:
- auth-service-deployment (2 replicas)
- user-service-deployment (2 replicas)
- todo-service-deployment (3 replicas)
- tag-service-deployment (2 replicas)
- notification-service-deployment (2 replicas + Celery workers)
- admin-service-deployment (2 replicas)

Services (ClusterIP):
- auth-service:5001
- user-service:5002
- todo-service:8081
- tag-service:8001
- notification-service:8002
- admin-service:8082

Ingress:
- api-gateway-ingress (Kong)
  Rules:
  - Host: api.todo-app.com
  - Paths: /* → kong-proxy:8000

StatefulSets:
- postgresql (6 instances)
- rabbitmq (3 replicas with quorum)
- redis (3 replicas with sentinel)

ConfigMaps:
- Service-specific configs

Secrets:
- Database credentials
- JWT signing keys
- VAPID keys
```

---

## Scalability & Performance

### Horizontal Scaling

**Auto-scaling Configuration:**

```yaml
Auth Service:
  minReplicas: 2
  maxReplicas: 10
  targetCPUUtilization: 70%
  
Todo Service:
  minReplicas: 3
  maxReplicas: 15
  targetCPUUtilization: 60%
  
Notification Service:
  minReplicas: 2
  maxReplicas: 8
  targetMemoryUtilization: 75%
```

### Caching Strategy

**Redis Caching Layers:**

1. **Gateway Level:**
   - JWKS keys (TTL: 1 hour)
   - Rate limiting counters

2. **Service Level:**
   - User profiles (TTL: 10 minutes)
   - Popular tags (TTL: 30 minutes)
   - Todo counts (TTL: 5 minutes)

### Performance Targets

| Metric | Target | Monitoring |
|--------|--------|------------|
| API Response Time (p95) | < 500ms | Prometheus |
| API Response Time (p99) | < 1s | Prometheus |
| Database Query Time (p95) | < 100ms | Prometheus |
| Error Rate | < 0.1% | Prometheus |
| Availability | 99.9% | Uptime checks |

---

## Observability

### Distributed Tracing (Jaeger)

**Trace Example:**

```text
Request ID: abc-123-def

Spans:
├── gateway-request (100ms)
│   ├── jwt-validation (10ms)
│   └── todo-service-call (85ms)
│       ├── database-query (60ms)
│       └── tag-service-call (20ms)
│           └── database-query (15ms)
└── response-serialization (5ms)

Total: 100ms
```

### Metrics (Prometheus)

**Key Metrics:**

```promql
# Request rate
rate(http_requests_total[5m])

# Latency
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))

# Error rate
rate(http_requests_total{status=~"5.."}[5m])

# Database connections
pg_stat_database_numbackends
```

### Logging (ELK Stack)

**Logging (future/optional): ELK stack for centralized log search; initial implementation may use simpler file/console logs.**

**Log Format:**

```json
{
  "timestamp": "2026-02-05T10:30:00Z",
  "level": "INFO",
  "service": "todo-service",
  "traceId": "abc-123-def",
  "spanId": "xyz-789",
  "userId": "user-uuid",
  "message": "Todo created successfully",
  "metadata": {
    "todoId": "todo-uuid",
    "duration_ms": 85
  }
}
```

---

## Disaster Recovery

### Backup Strategy

**Databases:**
- Automated daily backups (retained 30 days)
- Point-in-time recovery (7 days)
- Cross-region replication (production)

**Configuration:**
- Git repository (version controlled)
- ConfigMaps/Secrets backed up daily

### Recovery Time Objectives (RTO/RPO)

| Component | RTO | RPO |
|-----------|-----|-----|
| Services | 5 minutes | 0 (stateless) |
| Databases | 30 minutes | 1 hour |
| Message Queue | 10 minutes | 0 (replay events) |

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-05 | Architecture Team | Initial version |

---

**Next Steps:**
- [Low-Level Design](low-level-design.md) - Detailed implementation designs
- [Service Boundaries](service-boundaries.md) - Domain models and APIs
- [Sequence Diagrams](sequence-diagrams.md) - Interaction flows
