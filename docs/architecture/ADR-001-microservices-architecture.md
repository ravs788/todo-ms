# ADR-001: Polyglot Microservices Architecture

**Status:** Approved  
**Date:** 2026-03-02  
**Decision Makers:** Architecture Team  
**Supersedes:** Monolithic Spring Boot Architecture

---

## Context

This ADR documents the target architecture for the todo-ms learning system and serves as a reference for all subsequent design docs. The current Todo application backend is implemented as a monolithic Spring Boot application (React frontend is planned for future work). While this backend has served well for initial development, several factors motivate a migration to microservices:

### Current Pain Points

1. **Tight Coupling**: All business logic is tightly coupled within a single deployment unit
2. **Scaling Limitations**: Cannot scale individual components independently
3. **Technology Lock-in**: Entire backend must use Java/Spring Boot, which limits demonstrating polyglot patterns for this learning project
4. **Deployment Risk**: Any change requires redeploying the entire application
5. **Team Scalability**: Single codebase becomes harder to manage with multiple teams
6. **Learning Constraints**: Cannot demonstrate polyglot architecture patterns

### Business Requirements

1. **Independent Deployment**: Services should be deployable independently
2. **Technology Flexibility**: Use best-fit technologies per domain
3. **Scalability**: Scale services based on individual load patterns
4. **Fault Isolation**: Failure in one service should not cascade to others
5. **Learning Value**: Demonstrate modern microservices patterns with multiple tech stacks

---

## Decision

We will decompose the monolithic application into **7 independent microservices** using a polyglot architecture with the following technology distribution:

### Service Architecture

| Service | Language/Framework | Rationale | Port |
|---------|-------------------|-----------|------|
| **Authentication Service** | C# (.NET 8) | Excellent JWT/OAuth libraries, type safety, performance | 5001 |
| **User Management Service** | C# (.NET 8) | Share models with Auth, mature Entity Framework Core | 5002 |
| **Todo Service** | Java (Spring Boot 3.x) | Leverage existing codebase, rich ecosystem | 8081 |
| **Tag Service** | Python (FastAPI) | Lightweight, async-first, simple domain logic | 8001 |
| **Notification Service** | Python (FastAPI) | Excellent async support for I/O-bound operations | 8002 |
| **Admin Service** | Java (Spring Boot 3.x) | Can share code libraries with Todo Service | 8082 |
| **API Gateway** | Kong | Production-ready, extensive plugin ecosystem | 8000 |

*Initial implementation uses only PostgreSQL; MongoDB/Redis are explored as future polyglot persistence options.*

### Technology Rationale

#### Why C# (.NET 8) for Authentication/User Services?

**Strengths:**
- **Security Libraries**: Industry-leading JWT, OAuth, and cryptography support
- **Type Safety**: Strong typing reduces authentication bugs
- **Performance**: Native compilation and async/await model
- **Entity Framework Core**: Mature ORM with excellent migrations
- **Tooling**: Visual Studio, Rider, excellent debugging experience

**Use Cases:**
- Complex authentication flows (JWT issuance, JWKS, refresh tokens)
- User management with complex relationships
- Performance-critical security operations

#### Why Java (Spring Boot) for Todo/Admin Services?

**Strengths:**
- **Existing Code**: Leverage current monolithic codebase
- **Maturity**: Spring ecosystem is battle-tested
- **Spring Security**: Excellent JWT Resource Server support
- **Spring Cloud Stream**: Seamless RabbitMQ integration
- **Testing**: JUnit 5, Mockito, TestContainers

**Use Cases:**
- Core business logic (Todo CRUD)
- Admin operations requiring transactional integrity
- Services that need to share common domain models

#### Why Python (FastAPI) for Tag/Notification Services?

**Strengths:**
- **Async-First**: Native async/await for I/O-bound operations
- **Fast Development**: Rapid prototyping, less boilerplate
- **Celery Integration**: Excellent for background jobs (reminders)
- **WebPush Support**: PyWebPush for push notifications
- **Lightweight**: Ideal for simple, focused services

**Use Cases:**
- Tag management (simple CRUD, low complexity)
- Notification delivery (async, I/O-bound, background jobs)
- Services with heavy Python library dependencies

#### Why Kong for API Gateway?

**Strengths:**
- **Production-Ready**: Used by thousands of companies
- **Plugin Ecosystem**: Rate limiting, auth, logging, caching out-of-the-box
- **Performance**: Built on Nginx, handles high throughput
- **Kubernetes Native**: Excellent ingress controller
- **Declarative Config**: GitOps-friendly YAML configuration

**Alternatives Considered:**
- Spring Cloud Gateway (Java lock-in, less mature)
- Traefik (good, but less plugin ecosystem)
- Ambassador (Envoy-based, heavier)

---

## Communication Patterns

### Client-to-Gateway
- **Protocol**: REST/HTTP
- **Format**: JSON
- **Authentication**: JWT Bearer tokens in Authorization header

### Gateway-to-Services
- **Protocol**: REST/HTTP (can evolve to gRPC for performance)
- **Service Discovery**: Kubernetes DNS (native)
- **Load Balancing**: Kubernetes Service (round-robin)
- *Note: For local development, Docker Compose service names are used; for Kubernetes, service discovery relies on cluster DNS.*

### Inter-Service Events (Asynchronous)
- **Message Broker**: RabbitMQ
- **Pattern**: Pub/Sub with topic exchanges
- **Events**:
  - `user.approved` → Notification Service
  - `user.rejected` → Notification Service
  - `todo.created` → Notification Service (if reminder set)
  - `todo.completed` → Analytics (future)

### Why RabbitMQ over Kafka?

**RabbitMQ Advantages:**
- Simpler to deploy and operate
- Lower resource footprint
- Better for event-driven (not event streaming)
- Excellent Docker/K8s support
- Sufficient for our use case

**Kafka would be overkill** for:
- Our event volumes (< 1000 events/second)
- No need for event replay or stream processing
- Simpler operational model preferred

---

## Database Strategy

### Database per Service

Each service owns its database with **logical schema isolation** (initially) evolving to **physical isolation** (separate DB instances):

| Service | Database | Tables |
|---------|----------|--------|
| Auth Service | PostgreSQL (auth_db) | refresh_tokens, password_reset_log |
| User Service | PostgreSQL (user_db) | users, user_preferences |
| Todo Service | PostgreSQL (todo_db) | todos, todo_history |
| Tag Service | PostgreSQL (tag_db) | tags, todo_tags |
| Notification Service | PostgreSQL (notification_db) | push_subscriptions, notification_log |
| Admin Service | PostgreSQL (admin_db) | admin_actions (audit log) |

### Why PostgreSQL for All Services?

**Consistency:**
- Single database technology reduces operational complexity
- Shared expertise across team
- Consistent backup/restore procedures
- Uniform monitoring and tuning

**Feature Set:**
- ACID compliance for transactional integrity
- JSON/JSONB support for flexible schemas
- Excellent performance characteristics
- Mature ecosystem and tooling

**Future Flexibility:**
- Tag Service could migrate to MongoDB if document model fits better
- Notification Service could use Redis for real-time data
- Polyglot persistence remains an option

### Shared vs. Separate DB Instances

**Phase 1 (Initial):** 
- Single PostgreSQL instance with separate databases (schemas)
- Easier local development
- Lower infrastructure cost
- Faster migration from monolith

**Phase 2 (Production):**
- Separate PostgreSQL instances per service
- True isolation and independent scaling
- Fault isolation (DB failure doesn't affect all services)
- Independent backup schedules

---

## Deployment Architecture

### Containerization
- **Technology**: Docker
- **Base Images**: 
  - C#: `mcr.microsoft.com/dotnet/aspnet:8.0`
  - Java: `eclipse-temurin:21-jre-alpine`
  - Python: `python:3.12-slim`
- **Multi-stage Builds**: Yes, to minimize image size

### Orchestration
- **Local Development**: Docker Compose
- **Production**: Kubernetes (Minikube/Kind for local, AKS/EKS/GKE for cloud)
- *Target production environment: Kubernetes (AKS/EKS/GKE).*
- **Configuration Management**: Helm charts
- **Service Discovery**: Kubernetes DNS (automatic)

### Deployment Strategy
- **Pattern**: Blue-Green or Canary (per service)
- **Rollback**: Helm rollback or kubectl rollout undo
- **Health Checks**: Liveness and readiness probes on `/health` endpoint

---

## Security Architecture

### Authentication & Authorization

#### JWT Strategy
- **Algorithm**: RS256 (asymmetric signing)
- **Issuer**: Authentication Service
- **Audience**: All services
- **Claims**:
  ```json
  {
    "sub": "user-uuid",
    "username": "john_doe",
    "roles": ["USER"],
    "approved": true,
    "iat": 1234567890,
    "exp": 1234568790,
    "iss": "https://auth-service",
    "aud": "todo-api",
    "kid": "key-2026-03"
  }
  ```

#### JWKS (JSON Web Key Set)
- **Endpoint**: `GET /api/v1/auth/jwks`
- **Purpose**: Public key distribution for token validation
- **Rotation**: Keys rotated every 90 days, overlap period for grace
- **Consumers**: All services validate tokens using JWKS

#### Token Lifecycle
- **Access Token TTL**: 15 minutes
- **Refresh Token TTL**: 7 days
- **Revocation**: Short-lived tokens + optional blacklist in Redis

### API Gateway Security
- **Rate Limiting**: 
  - Per IP: 100 requests/minute
  - Per user (authenticated): 1000 requests/minute
- **CORS**: Configured for frontend domain only
- **Input Validation**: Schema validation via Kong plugins
- **Request Logging**: All requests logged with correlation ID

---

## Observability

### Distributed Tracing
- **Technology**: Jaeger (OpenTelemetry compatible)
- **Implementation**: OpenTelemetry SDKs in all services
- **Correlation**: X-Correlation-ID header propagated across all services
- **Sampling**: 100% in dev, 10% in production

### Metrics
- **Collection**: Prometheus
- **Visualization**: Grafana dashboards
- **Metrics**:
  - HTTP request rate, latency (p50, p95, p99)
  - Database connection pool usage
  - RabbitMQ queue depth
  - JWT validation success/failure rate
  - Service-specific business metrics

### Logging
- **Strategy**: *Planned centralized logging via ELK Stack.*
- **Format**: Structured JSON logs
- **Fields**: timestamp, level, service, trace_id, user_id, message
- **Aggregation**: Logstash → Elasticsearch → Kibana
- **Retention**: 30 days in Elasticsearch, 90 days in cold storage

---

## Testing Strategy

### Unit Testing
- **C# Services**: xUnit + Moq + FluentAssertions
- **Java Services**: JUnit 5 + Mockito + AssertJ
- **Python Services**: pytest + pytest-mock + pytest-cov
- **Coverage Target**: *Aspirational target of 80%+ per service for critical domain and integration paths.*

### Integration Testing
- **Technology**: Testcontainers (PostgreSQL, RabbitMQ, Redis)
- **Scope**: Database operations, event publishing/consuming
- **Execution**: CI pipeline per service

### Contract Testing
- **Technology**: Pact (Consumer-Driven Contracts)
- **Contracts**: Frontend ↔ Gateway, Gateway ↔ Services
- **Verification**: Provider tests run on service changes

### End-to-End Testing
- **Technology**: Playwright (existing test suite adapted)
- **Environment**: Docker Compose with all services
- **Scope**: Critical user journeys (registration, login, todo CRUD)

### Load Testing
- **Technology**: k6 (Grafana)
- **Scenarios**: Login, todo list, todo create
- **Thresholds**: p95 < 500ms, error rate < 1%

---

## Migration Approach

### Strategy: Strangler Fig Pattern

Gradually replace monolith functionality with microservices while keeping the system operational:

1. **Extract Authentication Service** → Route `/auth/*` to new service
2. **Extract User Service** → Route `/users/*` to new service
3. **Refactor Monolith into Todo Service** → Remove auth/user code
4. **Extract Tag Service** → Route `/tags/*` to new service
5. **Extract Notification Service** → Route `/notifications/*` to new service
6. **Extract Admin Service** → Route `/admin/*` to new service
7. **Decommission Monolith** → All traffic through microservices

*Note: The API Gateway (Kong) becomes the primary switch for routing traffic between the monolith and microservices during this migration.*

### Risk Mitigation
- **Feature Flags**: Kong routes can be toggled per endpoint
- **Shadow Deployment**: Run new services alongside monolith, compare responses
- **Gradual Cutover**: Route 10% → 50% → 100% of traffic to new services
- **Rollback Plan**: Revert Kong routes to monolith if issues detected

---

## Consequences

### Positive

1. **Independent Scalability**: Scale Auth Service (CPU-intensive) separately from Notification Service (I/O-bound)
2. **Technology Flexibility**: Use Python for async operations, C# for security-critical code
3. **Fault Isolation**: Tag Service failure doesn't affect Todo operations
4. **Team Autonomy**: Teams can own services with clear boundaries
5. **Faster Deployments**: Deploy Auth Service without affecting Todo Service
6. **Learning Value**: Demonstrates polyglot microservices in practice

### Negative

1. **Operational Complexity**: 7 services vs. 1 monolith to deploy and monitor
2. **Network Latency**: Inter-service calls add latency (mitigated by async events)
3. **Distributed Transactions**: No ACID across services (use eventual consistency)
4. **Debugging Complexity**: Tracing issues across services requires good observability
5. **Data Consistency**: Must handle eventual consistency between services
6. **Infrastructure Cost**: More containers, databases, and monitoring components

### Mitigation Strategies

1. **Observability First**: Invest in Jaeger, Prometheus and centralized logging upfront
2. **Service Mesh (Future)**: Consider Istio/Linkerd for advanced traffic management
3. **API Contracts**: Strict OpenAPI specs with contract testing
4. **Automation**: Comprehensive CI/CD pipelines for all services
5. **Documentation**: Detailed architecture docs and runbooks

---

## Alternatives Considered

### Alternative 1: Keep Monolith
**Rejected because:**
- Doesn't meet polyglot requirement
- Limits learning opportunities
- Doesn't address scaling and deployment concerns

### Alternative 2: Two-Service Split (User + Todo)
**Rejected because:**
- Too coarse-grained
- Limited benefits over monolith
- Doesn't showcase granular microservices

### Alternative 3: Serverless (AWS Lambda / Azure Functions)
**Rejected because:**
- Cold start latency issues
- Vendor lock-in
- More complex local development
- Doesn't meet Docker/K8s requirement

### Alternative 4: Single Language (Java Only)
**Rejected because:**
- Doesn't demonstrate polyglot architecture
- Misses learning opportunities with C# and Python
- Less flexibility for future technology choices

---

## References

- [Microservices Patterns (Sam Newman)](https://samnewman.io/books/building_microservices_2nd_edition/)
- [Domain-Driven Design (Eric Evans)](https://www.domainlanguage.com/ddd/)
- [.NET Microservices Architecture (Microsoft)](https://dotnet.microsoft.com/learn/aspnet/microservices-architecture)
- [Spring Microservices in Action (John Carnell)](https://www.manning.com/books/spring-microservices-in-action-second-edition)
- [FastAPI Best Practices](https://fastapi.tiangolo.com/tutorial/)
- [Kong Documentation](https://docs.konghq.com/)
- [The Twelve-Factor App](https://12factor.net/)

---

## Approval Sign-Off

| Name | Role | Date | Signature |
|------|------|------|-----------|
| Architecture Team | System Architect | 2026-03-02 | Approved |
| Development Team | Lead Developer | 2026-03-02 | Approved |
| Operations Team | DevOps Lead | 2026-03-02 | Approved |

---

**Document Version**: 1.0  
**Last Updated**: March 2, 2026  
**Next Review**: June 2, 2026 (3 months post-implementation)
