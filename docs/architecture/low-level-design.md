# Low-Level Design (LLD) - Todo Microservices Solution

**Version:** 1.0  
**Date:** March 2, 2026  
**Status:** Draft

This document provides a central overview of the low-level design for the Todo Microservices Solution. It outlines common patterns, infrastructure setup, and cross-cutting concerns that apply across all services. For detailed implementation of individual services, refer to the service-specific LLD documents below.

## Contents

1. [Common Patterns & Conventions](#common-patterns--conventions)
2. [Cross-Cutting Concerns](#cross-cutting-concerns)
3. [Configuration & Secrets Management](#configuration--secrets-management)
4. [Database Migrations](#database-migrations)
5. [Error Handling & API Responses](#error-handling--api-responses)
6. [Observability & Tracing](#observability--tracing)
7. [Security & Hardening Checklist](#security--hardening-checklist)
8. [Deployment Health Probes](#deployment-health-probes)
9. [Future Extensions](#future-extensions)
10. [Service-Specific LLD Documents](#service-specific-lld-documents)
11. [Document History](#document-history)

---

## Common Patterns & Conventions

### API Style
- **Protocol:** REST/HTTP
- **Content-Type:** application/json; charset=utf-8
- **Versioning:** All APIs under /api/v1/
- **Time:** All timestamps in UTC, stored as timestamptz
- **OpenAPI:** Each service exposes /swagger or /openapi.json
- **Health Checks:** GET /health (liveness), GET /ready (readiness)

### Data Modeling
- **IDs:** UUID for all entity identifiers
- **Auditing:** CreatedAt, UpdatedAt timestamps on mutable entities
- **Soft Deletes:** Not implemented; services use logical flags where appropriate

### Technology Stacks
- **C# Services:** ASP.NET Core 8, EF Core 8, FluentValidation
- **Java Services:** Spring Boot 3.2, Spring Data JPA, MapStruct, Flyway
- **Python Services:** FastAPI 0.110, SQLAlchemy 2.0, Pydantic v2, Alembic

---

## Cross-Cutting Concerns

### Authentication & Authorization
- **Authentication:** JWT Bearer tokens (RS256)
- **JWKS Endpoint:** Auth Service provides /api/v1/auth/jwks
- **Authorization:** Bearer <token> in Authorization header
- **Claims:** Standard JWT claims (sub, roles, approved) plus custom ones
- **RBAC:** Role-based access control (USER, ADMIN)

### Correlation & Idempotency
- **Correlation ID:** X-Correlation-Id header propagated across all services
- **Idempotency:** Idempotency-Key supported on POST create endpoints
- **Error Tracking:** All errors include traceId/correlationId

### Data Access Patterns
- **Pagination:** page, size query params; default size=10, max size=100
- **Sorting/Filtering:** Standard query params (e.g., sort=createdAt,desc)
- **Ownership:** Resources validated against authenticated user where applicable

### Messaging
- **Broker:** RabbitMQ for async event communication
- **Exchange Strategy:** Topic exchanges with routing keys like user.approved, todo.created
- **Serialization:** JSON for event payloads

### Rate Limiting
- **Gateway Level:** Kong handles global rate limiting
- **Service Level:** Services must handle 429 gracefully
- **Client Strategy:** Clients retry only idempotent GET operations

### Retry Policies
- **Background Workers:** Exponential backoff for failed operations
- **Idempotency:** Safe to retry POST create operations with idempotency key

---

## Configuration & Secrets Management

### Environment Variables
Services use environment variables for configuration:
- **Connection Strings:** Database connections
- **Feature Flags:** Toggle behaviors
- **External Service URLs:** Auth service JWKS, RabbitMQ, Redis
- **Security Keys:** VAPID keys, JWT signing keys

### Secrets Storage
- **Local Development:** .env files (excluded from VCS)
- **Kubernetes:** Secrets mounted as environment variables
- **Rotation:** Automated rotation for sensitive keys (e.g., JWT keys every 90 days)

### Configuration Management
- **.NET Services:** appsettings.json + Environment Variables
- **Java Services:** application.yml + Environment Variables
- **Python Services:** config.py + Environment Variables

---

## Database Migrations

### Migration Tools
- **.NET Services:** EF Core Migrations
- **Java Services:** Flyway
- **Python Services:** Alembic

### Migration Process
1. **Development:** Developers create migration scripts
2. **Review:** Code review of migration scripts
3. **Testing:** Migrations tested in staging environment
4. **Deployment:** Applied automatically on service startup (with fail-safe)

### Naming Conventions
- **Flyway:** V1__init.sql, V2__add_index_user_id.sql
- **Alembic:** autogenerate with descriptive names
- **EF Core:** descriptive names reflecting change

### Schema Management
- **Version Control:** All migration scripts in Git
- **Rollback Strategy:** Point-in-time recovery for databases
- **CI/CD:** Migrations validated as part of build pipeline

---

## Error Handling & API Responses

### Standard Error Envelope
All API responses use this structure:
```json
{
  "traceId": "uuid-or-correlation",
  "timestamp": "ISO-8601",
  "status": 400,
  "error": "Bad Request",
  "code": "VALIDATION_ERROR",
  "message": "Title is required",
  "details": [{ "field": "title", "issue": "must not be blank" }]
}
```

### Error Codes
| Code | HTTP Status | Description |
|------|-------------|-------------|
| VALIDATION_ERROR | 400 | Input validation failed |
| UNAUTHORIZED | 401 | Authentication failed |
| FORBIDDEN | 403 | Insufficient permissions |
| NOT_FOUND | 404 | Resource not found |
| CONFLICT | 409 | Resource conflict (e.g., duplicate) |
| INTERNAL_ERROR | 500 | Unexpected server error |

### Exception Mapping
- **.NET:** Custom middleware maps exceptions to standard envelope
- **Java:** @ControllerAdvice + @ExceptionHandler pattern
- **Python:** Middleware wraps HTTPExceptions

### Success Responses
- **201 Created:** For POST operations returning created resource
- **200 OK:** For successful GET/PUT/DELETE operations
- **204 No Content:** For successful DELETE operations without response body

---

## Observability & Tracing

### Distributed Tracing
- **Standard:** OpenTelemetry SDKs in all services
- **Propagated Headers:** traceparent, tracestate, X-Correlation-ID
- **Backend:** Jaeger for trace collection and visualization
- **Sampling:** 100% in development, 10% in production

### Metrics Collection
- **Backend:** Prometheus for metrics collection
- **Visualization:** Grafana dashboards
- **Key Metrics:**
  - HTTP request rates and latency (p50, p95, p99)
  - Database query times
  - Message queue depths
  - Business metrics (todos created, notifications sent)

### Logging
- **Format:** Structured JSON logs
- **Fields:** timestamp, level, service, traceId, spanId, message, metadata
- **Aggregation:** ELK stack (future/optional); initial implementation file/console
- **Correlation:** All logs include traceId

### OpenTelemetry Instrumentation
- **.NET:** OpenTelemetry.Extensions.Hosting + Console/OTLP exporters
- **Java:** OpenTelemetry Java Agent + Micrometer
- **Python:** opentelemetry-instrumentation packages

---

## Security & Hardening Checklist

### Input Validation
- [ ] All DTOs validated on entry
- [ ] Parameterized queries only (ORMs enforce this)
- [ ] Length restrictions on string inputs
- [ ] Type validation for all parameters

### Authentication & Authorization
- [ ] JWT validation via JWKS endpoint
- [ ] Claims validation (approved status, roles)
- [ ] Resource ownership checks where applicable
- [ ] ADMIN-only endpoints properly secured

### Data Protection
- [ ] Sensitive data encrypted at rest (database)
- [ ] Secrets never logged or exposed in responses
- [ ] TLS termination at ingress (Kong)
- [ ] CORS policies restricted to known origins

### API Security
- [ ] Rate limiting at gateway level
- [ ] Idempotency keys for state-changing operations
- [ ] Error messages don't leak sensitive information
- [ ] Health endpoints expose minimal data

### Operational Security
- [ ] Container images regularly scanned
- [ ] Dependencies kept up to date
- [ ] Security headers in responses
- [ ] Monitoring for suspicious activity

---

## Deployment Health Probes

### Liveness Probes
- **Endpoint:** GET /health
- **Response:** { "status": "healthy" }
- **Purpose:** Indicates if service is running

### Readiness Probes
- **Endpoint:** GET /ready
- **Checks:** Database connectivity, message broker, external dependencies
- **Purpose:** Indicates if service is ready to accept traffic

### Graceful Shutdown
- **.NET:** IHostApplicationLifetime.Stopping event
- **Java:** SpringApplication.exit() + graceful shutdown period
- **Python:** FastAPI lifespan handlers

---

## Future Extensions

### Gateway Enhancements
- Move JWT/OIDC validation to Kong when feasible
- Implement API rate limiting at service level
- Add request/response transformation plugins

### Additional Services
- **Analytics Service:** Python/Go service consuming events
- **Search Service:** Elasticsearch for todo search
- **File Service:** Handle attachments (future enhancement)

### Infrastructure
- Introduce service mesh (Istio) for mTLS and traffic shaping
- Implement canary deployments with traffic shifting
- Add advanced monitoring and alerting

### Data
- Implement CQRS pattern for read/write separation
- Add read models for complex queries
- Consider event sourcing for audit trails

---

## Service-Specific LLD Documents

For detailed implementation of each service, refer to:

### Core Services
- [Authentication Service LLD](low-level-design-auth-service.md) - .NET 8 authentication, JWT management
- [User Management Service LLD](low-level-design-user-service.md) - .NET 8 user profiles and preferences
- [Todo Service LLD](low-level-design-todo-service.md) - Spring Boot todo CRUD and business logic
- [Tag Service LLD](low-level-design-tag-service.md) - FastAPI tag management

### Supporting Services
- [Notification Service LLD](low-level-design-notification-service.md) - FastAPI + Celery push notifications
- [Admin Service LLD](low-level-design-admin-service.md) - Spring Boot admin operations
- [API Gateway LLD](low-level-design-gateway.md) - Kong configuration and routing

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-02 | Architecture Team | Initial LLD overview drafted |

---

**Next Steps:**
- [Low-Level Design](low-level-design-auth-service.md) - Authentication Service implementation details
- [Low-Level Design](low-level-design-user-service.md) - User Management Service implementation details
- [Low-Level Design](low-level-design-todo-service.md) - Todo Service implementation details
- [Low-Level Design](low-level-design-tag-service.md) - Tag Service implementation details
