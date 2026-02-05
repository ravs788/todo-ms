# Migration Plan - Monolith to Microservices

**Version:** 1.0  
**Date:** February 5, 2026  
**Status:** Approved  
**Estimated Duration:** 18 weeks

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Migration Strategy](#migration-strategy)
3. [Phase Breakdown](#phase-breakdown)
4. [Risk Management](#risk-management)
5. [Rollback Procedures](#rollback-procedures)
6. [Success Metrics](#success-metrics)
7. [Timeline & Milestones](#timeline--milestones)

---

## Executive Summary

This document outlines the phased migration approach to transform the monolithic Todo application into a polyglot microservices architecture. The migration follows the **Strangler Fig Pattern** to minimize risk and ensure continuous system availability.

### Key Principles

1. **Incremental Migration**: Services extracted one at a time
2. **Zero Downtime**: System remains operational throughout migration
3. **Rollback Ready**: Each phase can be rolled back independently
4. **Continuous Testing**: E2E tests run after each phase
5. **Data Integrity**: No data loss during migration

---

## Migration Strategy

### Strangler Fig Pattern

The Strangler Fig pattern gradually replaces the monolith by routing traffic to new services while keeping the old system operational:

```
Phase 1: Monolith handles everything
┌──────────────────────────────────┐
│        Todo Monolith             │
│  - Auth                          │
│  - User Management               │
│  - Todo CRUD                     │
│  - Tag Management                │
│  - Notifications                 │
│  - Admin Operations              │
└──────────────────────────────────┘

Phase 2: Auth Service extracted
┌──────────────┐    ┌──────────────┐
│ Auth Service │    │   Monolith   │
│   (C# .NET)  │    │  - Users     │
│              │    │  - Todos     │
└──────────────┘    │  - Tags      │
                    │  - Notifs    │
                    │  - Admin     │
                    └──────────────┘

Phase N: Fully decomposed
┌────────┐ ┌────────┐ ┌────────┐
│  Auth  │ │  User  │ │  Todo  │
│  (C#)  │ │  (C#)  │ │ (Java) │
└────────┘ └────────┘ └────────┘
┌────────┐ ┌────────┐ ┌────────┐
│  Tag   │ │ Notify │ │ Admin  │
│(Python)│ │(Python)│ │ (Java) │
└────────┘ └────────┘ └────────┘
```

---

## Phase Breakdown

### Phase 0: Preparation (Week 1-2)

**Goal:** Set up infrastructure and tooling

**Tasks:**
- [ ] Create `todo-ms` repository/folder
- [ ] Set up Docker Compose for local development
- [ ] Configure PostgreSQL instances (6 databases)
- [ ] Set up Redis (caching/sessions)
- [ ] Set up RabbitMQ (message broker)
- [ ] Configure Kong API Gateway
- [ ] Set up observability stack (Prometheus, Grafana, Jaeger, ELK)
- [ ] Create CI/CD pipeline skeleton
- [ ] Document environment setup

**Deliverables:**
- Infrastructure as Code (docker-compose.yml)
- Setup scripts
- Documentation

**Success Criteria:**
- All infrastructure services start successfully
- Health checks pass for all components
- Team can run infrastructure locally

---

### Phase 1: Extract Authentication Service (Week 3-4)

**Goal:** Create C# Authentication Service and route `/auth` traffic to it

#### 1.1 Service Development

**Tasks:**
- [ ] Create C# .NET 8 Web API project
- [ ] Implement user authentication endpoints
  - `POST /api/v1/auth/login`
  - `POST /api/v1/auth/register`
  - `POST /api/v1/auth/refresh`
  - `POST /api/v1/auth/logout`
  - `POST /api/v1/auth/forgot-password`
- [ ] Implement JWT generation (RS256)
- [ ] Implement JWKS endpoint (`GET /api/v1/auth/jwks`)
- [ ] Implement password hashing (BCrypt)
- [ ] Set up Entity Framework Core
- [ ] Create database migrations
- [ ] Implement refresh token management
- [ ] Add structured logging
- [ ] Add OpenTelemetry tracing

**Database Schema (auth_db):**
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

#### 1.2 Testing

**Tasks:**
- [ ] Unit tests (80%+ coverage)
  - Token generation
  - Token validation
  - JWKS generation
  - Password hashing
- [ ] Integration tests
  - Database operations
  - Redis integration
  - JWT end-to-end flow
- [ ] Load tests
  - Login endpoint: 100 req/s
  - Success criteria: p95 < 500ms

#### 1.3 Deployment

**Tasks:**
- [ ] Create Dockerfile
- [ ] Add to docker-compose.yml
- [ ] Configure Kong route: `/api/v1/auth/*` → `auth-service:5001`
- [ ] Deploy to local environment
- [ ] Verify health checks
- [ ] Run E2E tests

#### 1.4 Data Migration

**Tasks:**
- [ ] Copy user credentials from monolith to auth_db
- [ ] Verify password hashes are compatible
- [ ] Test login with migrated users

**Rollback Plan:**
- Revert Kong routes to monolith
- Keep auth service running for testing

---

### Phase 2: Extract User Management Service (Week 5)

**Goal:** Create C# User Service and route `/users` traffic to it

#### 2.1 Service Development

**Tasks:**
- [ ] Create C# .NET 8 Web API project
- [ ] Implement user management endpoints
  - `GET /api/v1/users/me`
  - `PUT /api/v1/users/me`
  - `GET /api/v1/users/{id}` (Admin)
  - `GET /api/v1/users` (Admin, paginated)
  - `PUT /api/v1/users/{id}/preferences`
- [ ] Set up JWT validation via JWKS
- [ ] Implement Entity Framework Core
- [ ] Create database migrations
- [ ] Add structured logging and tracing

**Database Schema (user_db):**
```sql
CREATE TABLE users (
  id UUID PRIMARY KEY,
  username VARCHAR(100) UNIQUE NOT NULL,
  email VARCHAR(255) UNIQUE NOT NULL,
  status VARCHAR(20) DEFAULT 'PENDING',
  roles TEXT[] DEFAULT '{\"USER\"}',
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

#### 2.2 Testing & Deployment

**Tasks:**
- [ ] Unit tests (80%+ coverage)
- [ ] Integration tests
- [ ] Create Dockerfile
- [ ] Configure Kong route: `/api/v1/users/*` → `user-service:5002`
- [ ] Deploy and verify

#### 2.3 Data Migration

**Tasks:**
- [ ] Migrate user profiles from monolith to user_db
- [ ] Verify data integrity

---

### Phase 3: Refactor Monolith into Todo Service (Week 6-7)

**Goal:** Remove auth/user code from monolith, configure as JWT Resource Server

#### 3.1 Code Refactoring

**Tasks:**
- [ ] Remove `AuthController`, `AdminController` (moved to separate services)
- [ ] Remove `UserRepository` and user domain code
- [ ] Configure Spring Security as OAuth2 Resource Server
- [ ] Implement JWKS-based JWT validation
- [ ] Extract user ID from JWT claims
- [ ] Add RabbitMQ event publishing
  - `todo.created`
  - `todo.updated`
  - `todo.completed`
  - `todo.deleted`
- [ ] Update tests to use test JWT tokens

**Configuration:**
```yaml
spring:
  security:
    oauth2:
      resourceserver:
        jwt:
          jwk-set-uri: http://auth-service:5001/api/v1/auth/jwks
```

#### 3.2 Testing & Deployment

**Tasks:**
- [ ] Update unit tests
- [ ] Update integration tests
- [ ] Run full E2E test suite
- [ ] Configure Kong route: `/api/v1/todos/*` → `todo-service:8081`
- [ ] Deploy and verify

---

### Phase 4: Extract Tag Service (Week 8)

**Goal:** Create Python FastAPI Tag Service

#### 4.1 Service Development

**Tasks:**
- [ ] Create Python FastAPI project
- [ ] Implement tag endpoints
  - `GET /api/v1/tags`
  - `POST /api/v1/tags`
  - `PUT /api/v1/tags/{id}`
  - `DELETE /api/v1/tags/{id}`
  - `GET /api/v1/tags/popular`
- [ ] Implement JWT middleware
- [ ] Set up SQLAlchemy + Alembic
- [ ] Create database migrations
- [ ] Add structured logging

**Database Schema (tag_db):**
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

#### 4.2 Testing & Deployment

**Tasks:**
- [ ] Unit tests (pytest, 80%+ coverage)
- [ ] Integration tests (Testcontainers)
- [ ] Create Dockerfile
- [ ] Configure Kong route: `/api/v1/tags/*` → `tag-service:8001`
- [ ] Deploy and verify

#### 4.3 Data Migration

**Tasks:**
- [ ] Migrate tags from monolith to tag_db
- [ ] Migrate todo-tag associations

---

### Phase 5: Extract Notification Service (Week 9-10)

**Goal:** Create Python FastAPI Notification Service with Celery

#### 5.1 Service Development

**Tasks:**
- [ ] Create Python FastAPI project
- [ ] Implement notification endpoints
  - `POST /api/v1/notifications/subscribe`
  - `POST /api/v1/notifications/unsubscribe`
  - `GET /api/v1/notifications/history`
- [ ] Set up Celery with Redis broker
- [ ] Implement PyWebPush for VAPID notifications
- [ ] Implement APScheduler for reminder scheduling
- [ ] Implement RabbitMQ consumers
  - `todo.created` event → Schedule reminder
  - `user.approved` event → Send welcome notification
- [ ] Set up SQLAlchemy + Alembic

**Database Schema (notification_db):**
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

#### 5.2 Testing & Deployment

**Tasks:**
- [ ] Unit tests (pytest, 80%+ coverage)
- [ ] Integration tests (RabbitMQ, Redis, Celery)
- [ ] Create Dockerfile (API + Celery worker)
- [ ] Configure Kong route: `/api/v1/notifications/*` → `notification-service:8002`
- [ ] Deploy and verify

#### 5.3 Data Migration

**Tasks:**
- [ ] Migrate push subscriptions from monolith

---

### Phase 6: Extract Admin Service (Week 11)

**Goal:** Create Java Spring Boot Admin Service

#### 6.1 Service Development

**Tasks:**
- [ ] Create Spring Boot 3.x project
- [ ] Implement admin endpoints
  - `GET /api/v1/admin/users/pending`
  - `POST /api/v1/admin/users/{id}/approve`
  - `POST /api/v1/admin/users/{id}/reject`
  - `GET /api/v1/admin/stats`
  - `GET /api/v1/admin/audit`
- [ ] Publish RabbitMQ events
  - `user.approved`
  - `user.rejected`
- [ ] Set up Spring Data JPA
- [ ] Create Flyway migrations

**Database Schema (admin_db):**
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

#### 6.2 Testing & Deployment

**Tasks:**
- [ ] Unit tests (JUnit 5, 80%+ coverage)
- [ ] Integration tests
- [ ] Create Dockerfile
- [ ] Configure Kong route: `/api/v1/admin/*` → `admin-service:8082`
- [ ] Deploy and verify

---

### Phase 7: Frontend Migration (Week 12)

**Goal:** Update React frontend to call Kong Gateway

#### 7.1 Frontend Updates

**Tasks:**
- [ ] Update API base URL to `http://localhost:8000`
- [ ] Update service files
  - `authService.js` → `/api/v1/auth/*`
  - `todoService.js` → `/api/v1/todos/*`
  - `tagService.js` → `/api/v1/tags/*`
  - `notificationService.js` → `/api/v1/notifications/*`
  - `adminService.js` → `/api/v1/admin/*`
- [ ] Update environment variables
- [ ] Test all user flows
- [ ] Create Dockerfile (Nginx)

#### 7.2 Testing

**Tasks:**
- [ ] Run all E2E Playwright tests
- [ ] Manual testing of critical paths
- [ ] Browser compatibility testing

---

### Phase 8: Testing & Quality Assurance (Week 13-14)

**Goal:** Comprehensive testing across all services

#### 8.1 Test Execution

**Tasks:**
- [ ] Run all unit tests across services
- [ ] Run all integration tests
- [ ] Run contract tests (Pact)
- [ ] Run E2E tests (Playwright)
- [ ] Run load tests (k6)
- [ ] Run security tests (OWASP ZAP, Trivy)
- [ ] Fix identified issues
- [ ] Verify test coverage targets (80%+)

#### 8.2 Performance Testing

**Tasks:**
- [ ] Load test: 500 concurrent users
- [ ] Stress test: Find breaking point
- [ ] Endurance test: 8-hour sustained load
- [ ] Analyze bottlenecks
- [ ] Optimize if needed

---

### Phase 9: Kubernetes Deployment (Week 15-16)

**Goal:** Deploy to Kubernetes cluster

#### 9.1 Kubernetes Manifests

**Tasks:**
- [ ] Create Deployment manifests for all services
- [ ] Create Service manifests (ClusterIP)
- [ ] Create Ingress manifest (Kong)
- [ ] Create ConfigMaps
- [ ] Create Secrets
- [ ] Create HorizontalPodAutoscaler
- [ ] Create PersistentVolumeClaims (databases)

#### 9.2 Helm Chart

**Tasks:**
- [ ] Create Helm chart structure
- [ ] Define values.yaml
- [ ] Create templates
- [ ] Test Helm deployment
- [ ] Document Helm usage

#### 9.3 Deployment

**Tasks:**
- [ ] Deploy to Minikube/Kind (local)
- [ ] Verify all pods running
- [ ] Test via Ingress
- [ ] Run E2E tests against K8s deployment
- [ ] Document deployment procedures

---

### Phase 10: Observability & Monitoring (Week 17)

**Goal:** Set up comprehensive observability

#### 10.1 Distributed Tracing

**Tasks:**
- [ ] Configure Jaeger
- [ ] Add OpenTelemetry to all services
- [ ] Verify trace propagation
- [ ] Create Jaeger dashboards

#### 10.2 Metrics & Monitoring

**Tasks:**
- [ ] Configure Prometheus
- [ ] Add metrics exporters to all services
- [ ] Create Grafana dashboards
  - Service health overview
  - Request rates & latency
  - Database metrics
  - RabbitMQ metrics
- [ ] Set up alerting rules

#### 10.3 Centralized Logging

**Tasks:**
- [ ] Configure ELK Stack
- [ ] Configure log forwarding from all services
- [ ] Create Kibana dashboards
- [ ] Document log analysis procedures

---

### Phase 11: Production Readiness (Week 18)

**Goal:** Final preparation for production deployment

#### 11.1 Security Hardening

**Tasks:**
- [ ] Secrets management (Kubernetes secrets or Azure Key Vault)
- [ ] Network policies
- [ ] mTLS configuration (optional)
- [ ] Security audit
- [ ] Penetration testing

#### 11.2 Backup & Disaster Recovery

**Tasks:**
- [ ] Database backup procedures
- [ ] Point-in-time recovery testing
- [ ] Disaster recovery runbook
- [ ] Test restore procedures

#### 11.3 Documentation

**Tasks:**
- [ ] Complete architecture documentation
- [ ] Deployment runbooks
- [ ] Troubleshooting guides
- [ ] API documentation (OpenAPI/Swagger)
- [ ] Team training sessions

#### 11.4 Go-Live Checklist

**Tasks:**
- [ ] All tests passing (unit, integration, E2E, load)
- [ ] Security scans clean
- [ ] Performance benchmarks met
- [ ] Monitoring and alerting configured
- [ ] Backup procedures tested
- [ ] Rollback procedures documented and tested
- [ ] Team trained
- [ ] Stakeholders notified

---

## Risk Management

### Risk Matrix

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Data loss during migration | High | Low | Backup before migration, test migrations |
| Service downtime | High | Medium | Strangler pattern, feature flags, rollback plan |
| JWT incompatibility | Medium | Low | Test JWT validation early, use standard libraries |
| Performance degradation | Medium | Medium | Load testing, caching, horizontal scaling |
| Database connection exhaustion | Medium | Medium | Connection pooling, circuit breakers |
| Message queue backlog | Medium | Low | Monitor queue depth, scale consumers |
| Security vulnerabilities | High | Low | Automated scanning, penetration testing |

### Mitigation Strategies

1. **Comprehensive Testing**: Test each phase thoroughly before proceeding
2. **Feature Flags**: Use Kong to toggle routes between monolith and microservices
3. **Shadow Deployment**: Run new services alongside monolith for comparison
4. **Gradual Rollout**: Route 10% → 50% → 100% of traffic to new services
5. **Monitoring**: Real-time monitoring with alerts for anomalies

---

## Rollback Procedures

### Per-Phase Rollback

Each phase can be rolled back independently:

**Phase 1 (Auth Service):**
```bash
# Revert Kong route to monolith
kubectl apply -f kong-config-monolith.yaml

# Stop auth service
kubectl scale deployment auth-service --replicas=0
```

**Phase 3 (Todo Service):**
```bash
# Revert Kong route to monolith
kubectl apply -f kong-config-monolith.yaml

# Redeploy monolith with full functionality
kubectl apply -f monolith-deployment.yaml
```

### Full Rollback

In case of critical issues:

```bash
# Stop all microservices
kubectl delete namespace todo-app

# Redeploy monolith
kubectl apply -f monolith-full-stack.yaml

# Verify monolith health
kubectl get pods -n todo-monolith
```

---

## Success Metrics

### Technical Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Service Availability | 99.9% | Uptime monitoring |
| API Response Time (p95) | < 500ms | Prometheus |
| API Response Time (p99) | < 1s | Prometheus |
| Error Rate | < 0.1% | Prometheus |
| Test Coverage | 80%+ | SonarQube |
| Deployment Frequency | 5+/week | CI/CD metrics |
| Mean Time to Recovery (MTTR) | < 30 min | Incident logs |

### Business Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Zero Data Loss | 100% | Audit logs |
| Zero Downtime | 100% | Uptime monitoring |
| User Satisfaction | > 4.5/5 | User surveys |

---

## Timeline & Milestones

```
Week 1-2   : Phase 0 - Infrastructure Setup
Week 3-4   : Phase 1 - Auth Service
Week 5     : Phase 2 - User Service
Week 6-7   : Phase 3 - Todo Service Refactoring
Week 8     : Phase 4 - Tag Service
Week 9-10  : Phase 5 - Notification Service
Week 11    : Phase 6 - Admin Service
Week 12    : Phase 7 - Frontend Migration
Week 13-14 : Phase 8 - Testing & QA
Week 15-16 : Phase 9 - Kubernetes Deployment
Week 17    : Phase 10 - Observability
Week 18    : Phase 11 - Production Readiness
```

### Key Milestones

- **Week 2**: Infrastructure ready
- **Week 4**: First microservice (Auth) deployed
- **Week 7**: Monolith refactored
- **Week 11**: All microservices deployed
- **Week 14**: Testing complete
- **Week 16**: Kubernetes deployment complete
- **Week 18**: Production-ready

---

## Post-Migration

### Monitoring Period (Week 19-22)

**Tasks:**
- Monitor system performance
- Address any issues
- Optimize based on real-world usage
- Gather user feedback
- Plan Phase 2 improvements

### Phase 2 Enhancements (Future)

**Potential Improvements:**
- Service Mesh (Istio/Linkerd)
- Event Sourcing for audit trails
- CQRS for read-heavy operations
- GraphQL API Gateway
- Advanced caching strategies

---

**Document Version:** 1.0  
**Last Updated:** February 5, 2026  
**Owner:** Architecture Team  
**Next Review:** Post-migration retrospective
