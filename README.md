# Todo Microservices Solution

A polyglot microservices architecture implementation of a Todo application using C# (.NET 8), Java (Spring Boot 3), and Python (FastAPI), fronted by an API Gateway (Kong), and instrumented for observability.

What problem does this solve?
- Demonstrates how to decompose a monolith into properly bounded microservices with clear contracts, independent data stores, asynchronous messaging, and production‑grade DevOps practices (containerization, orchestration, observability, testing). Users can register, get approved, create and tag todos, and receive reminders; architects and engineers can learn/extend the system with confidence.

Intended audience
- Learners exploring microservices by example (architecture, boundaries, tradeoffs)
- Backend/SDETs needing a structured, testable reference implementation
- Architects/DevOps evaluating patterns (contracts, deployments, observability)

Quick links
- Architecture: docs/architecture/
  - High-Level Design: docs/architecture/high-level-design.md
  - Low-Level Design: docs/architecture/low-level-design.md
  - Service Boundaries: docs/architecture/service-boundaries.md
  - Sequence Diagrams: docs/architecture/sequence-diagrams.md
- Testing strategy: docs/testing/test-strategy.md
- Deployment plans: docs/deployment/migration-plan.md

---

Architecture snapshot
- API Gateway: Kong (DB-less, declarative), routing /api/v1/* to services
- Services:
  - auth-service (C# .NET 8) – authentication, JWT/JWKS
  - user-service (C# .NET 8) – user profile, preferences
  - todo-service (Spring Boot) – todo CRUD, history, events
  - tag-service (FastAPI) – tags and associations
  - notification-service (FastAPI + Celery) – push/reminders, consumes events
  - admin-service (Spring Boot) – approvals, audit, publishes events
- Infra: PostgreSQL per service, Redis (cache/broker), RabbitMQ (events)
- Observability: Jaeger tracing, Prometheus + Grafana metrics
- Contracts: OpenAPI per service, AsyncAPI + JSON Schemas for events (to be added under docs/contracts)

End‑to‑end example flow (docs)
- Creating a Todo: Frontend → Kong → Auth (JWT) → Todo (create) → RabbitMQ (todo.created) → Notification (schedules reminder). See docs/architecture/sequence-diagrams.md and docs/architecture/high-level-design.md for request and event flows.

---

Project structure (current)
```
todo-ms/
├── services/
│   ├── auth-service/              # .NET 8 (API/Application/Infrastructure, tests/)
│   ├── user-service/              # .NET 8
│   ├── todo-service/              # Spring Boot
│   ├── tag-service/               # FastAPI
│   ├── notification-service/      # FastAPI + Celery
│   └── admin-service/             # Spring Boot
│
├── gateway/
│   └── kong/
│       └── kong.yml               # DB-less declarative config
│
├── infra/
│   └── compose/
│       ├── docker-compose.yml     # Local infra stack (DBs, MQ, Redis, Kong, Jaeger, Prometheus, Grafana)
│       └── prometheus/
│           └── prometheus.yml
│
├── docs/
│   ├── architecture/              # ADR/HLD/LLD/Boundaries/Sequences
│   ├── testing/                   # Test strategy
│   └── deployment/                # Plans and runbooks
│
├── scripts/                       # dev/test/migrate scripts
├── .env.example                   # Global defaults (DB names, log levels, etc.)
├── Makefile                       # up/down/logs/test/migrate targets
└── README.md
```

---

Getting started (local)
Prerequisites
- Docker 24+, Docker Compose 2.20+
- Make (for convenience)

1) Configure environment
```
cp .env.example .env
# Adjust ports/credentials only if needed
```

2) Start local infrastructure (databases, MQ, cache, gateway, observability)
```
make up
# or: docker compose -f infra/compose/docker-compose.yml up -d
```

3) Access local UIs
- Kong proxy: http://localhost:8000
- Kong Admin API: http://localhost:8001
- RabbitMQ: http://localhost:15672
- Jaeger: http://localhost:16686
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3001

Note: Service containers will be added as we implement each service. The compose stack currently provisions infra so you can run services locally against it.

Configuration and environment
- Global defaults live in .env.example (copied to .env).
- Per-service configuration (to be added with code) follows LLD:
  - JWT/JWKS: Services validate JWT against Auth JWKS (http://auth-service:5001/api/v1/auth/jwks in compose network)
  - Databases: Postgres instances are exposed on host ports (per HLD) and reachable by service names inside the compose network (e.g., postgres-todo:5432)
  - RabbitMQ: rabbitmq:5672
  - Redis: redis:6379
- For Kubernetes, use secrets/ConfigMaps/Helm values (see docs/deployment/), once charts/manifests are introduced.

Testing (TDD-first)
- Unit/Integration/Contract/E2E testing plans are outlined in docs/testing/test-strategy.md.
- Run tests centrally:
```
bash scripts/test.sh
```
Preconditions
- For integration and contract tests that touch DB/MQ, ensure infra is up (make up).
- For load/E2E (to be added), ensure services are built and running; provide base URLs via env.

Observability workflow
- When services are running, each request should carry X-Correlation-Id and traceparent.
- You can explore distributed traces in Jaeger (service dropdowns match service names).
- Prometheus scrapes service metrics; Grafana provides dashboards (import or add via UI).

Directory-to-role map
| Role | Start here | Why |
|------|------------|-----|
| New backend dev | docs/architecture/high-level-design.md | Big picture, service responsibilities |
| QA / SDET | docs/testing/test-strategy.md | Coverage strategy, how to run tests |
| DevOps | infra/compose/docker-compose.yml | Local infra services and ports |
| Architects | docs/architecture/service-boundaries.md | Clear bounded contexts and rules |

Roadmap
- docs/contracts with OpenAPI per service and AsyncAPI + JSON Schemas for events
- Service code scaffolds with correlation and error envelope middlewares
- CI workflows for contract validation and container builds
- Compose services and Kubernetes manifests/Helm charts

Last updated: March 4, 2026
