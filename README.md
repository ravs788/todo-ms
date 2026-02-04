<!-- Title & Badges -->
<div align="center">

# Todo Microservices Solution

A polyglot microservices architecture implementation of a Todo application using C# (.NET 8), Java (Spring Boot 3), and Python (FastAPI), fronted by an API Gateway (Kong), and instrumented for observability.

<!-- Core stack badges (only what exists in this repo today) -->
  
[![Architecture: Microservices](https://img.shields.io/badge/Architecture-Microservices-4B9CD3)](docs/architecture/)
![Docker Compose](https://img.shields.io/badge/Docker%20Compose-Ready-2496ED?logo=docker&logoColor=white)
![Kong](https://img.shields.io/badge/Kong-DB--less%20Gateway-023430)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-Events-FF6600?logo=rabbitmq&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-Cache/Broker-DC382D?logo=redis&logoColor=white)
![Jaeger](https://img.shields.io/badge/Jaeger-Tracing-FFBF00)
![Prometheus](https://img.shields.io/badge/Prometheus-Metrics-E6522C?logo=prometheus&logoColor=white)
![Grafana](https://img.shields.io/badge/Grafana-Dashboards-F46800?logo=grafana&logoColor=white)

</div>

---

Status: Architecture and infra ready; service implementations in progress.

## 🧭 What problem does this solve?
- Demonstrates decomposition of a monolith into bounded microservices with independent data stores, async messaging, and production‑grade DevOps (containerization, orchestration, observability, testing).
- Provides a realistic Todo domain where users register, get approved, manage tagged todos, and receive reminders, so engineers can practice extending and testing the system safely.

## 👥 Intended audience
- Learners exploring microservices by example (architecture, boundaries, tradeoffs)
- Backend/SDETs needing a structured, testable reference implementation
- Architects/DevOps evaluating patterns (contracts, deployments, observability)
- Tech leads designing microservice‑aligned test strategies

## 🔗 Quick links
- Architecture: docs/architecture/
  - High‑Level Design: docs/architecture/high-level-design.md
  - Low‑Level Design: docs/architecture/low-level-design.md
  - Service Boundaries: docs/architecture/service-boundaries.md
  - Sequence Diagrams: docs/architecture/sequence-diagrams.md
- Testing strategy: docs/testing/test-strategy.md
- Deployment plans: docs/deployment/migration-plan.md

---

## 🏛️ Architecture snapshot
- API Gateway: Kong (DB‑less, declarative), routing /api/v1/* to services
- Services:
  - auth-service (C# .NET 8) – authentication, JWT/JWKS
  - user-service (C# .NET 8) – user profile, preferences
  - todo-service (Spring Boot) – todo CRUD, history, events
  - tag-service (FastAPI) – tags and associations
  - notification-service (FastAPI + Celery) – push/reminders, consumes events
  - admin-service (Spring Boot) – approvals, audit, publishes events
- Infra: PostgreSQL per service, Redis (cache/broker), RabbitMQ (events)
- Observability: Jaeger tracing, Prometheus + Grafana metrics
- Contracts: OpenAPI per service, AsyncAPI + JSON Schemas for events (planned under docs/contracts)

### 🔁 End‑to‑end example flow (docs)
Creating a Todo: Frontend → Kong → Auth (JWT) → Todo (create) → RabbitMQ (todo.created) → Notification (schedules reminder).  
See docs/architecture/sequence-diagrams.md and docs/architecture/high-level-design.md for request and event flows.

---

## 📁 Project structure (current)
```text
todo-ms/
  ├── services/
  │   ├── auth-service/              # .NET 8 (API/Application/Infrastructure, tests/)
  │   ├── user-service/              # .NET 8
  │   ├── todo-service/              # Spring Boot
  │   ├── tag-service/               # FastAPI
  │   ├── notification-service/      # FastAPI + Celery
  │   └── admin-service/             # Spring Boot
  ├── gateway/
  │   └── kong/
  │       └── kong.yml               # DB-less declarative config
  ├── infra/
  │   └── compose/
  │       ├── docker-compose.yml     # Local infra stack (DBs, MQ, Redis, Kong, Jaeger, Prometheus, Grafana)
  │       └── prometheus/
  │           └── prometheus.yml
  ├── docs/
  │   ├── architecture/              # ADR/HLD/LLD/Boundaries/Sequences
  │   ├── testing/                   # Test strategy
  │   └── deployment/                # Plans and runbooks
  ├── scripts/                       # dev/test/migrate scripts
  ├── .env.example                   # Global defaults (DB names, log levels, etc.)
  ├── Makefile                       # up/down/logs/test/migrate targets
  └── README.md
```

## ⚡ Getting started (local)

### ✅ Prerequisites
- Docker 24+, Docker Compose 2.20+
- Make (for convenience)

1) Configure environment
```bash
cp .env.example .env
# Adjust ports/credentials only if needed
```

2) Start local infrastructure (databases, MQ, cache, gateway, observability)
```bash
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

## 🔧 Configuration and environment
- Global defaults live in .env.example (copy to .env).
- Per‑service configuration (as code is added) follows:
  - JWT/JWKS: Services validate JWT against Auth JWKS (http://auth-service:5001/api/v1/auth/jwks) using compose network DNS names (e.g., auth-service, postgres-todo).
  - Databases: Postgres instances exposed on host ports (per HLD) and reachable by service name inside the compose network (e.g., postgres-todo:5432).
  - RabbitMQ: rabbitmq:5672
  - Redis: redis:6379
- For Kubernetes, use Secrets/ConfigMaps/Helm values (see docs/deployment/).
- References: High‑Level Design (docs/architecture/high-level-design.md), Low‑Level Design (docs/architecture/low-level-design.md)

## 🧪 Testing (TDD‑first)
- Initially focuses on architecture‑level tests and infra health checks; service‑level tests are added as each service is implemented.
- Run tests centrally:
```bash
bash scripts/test.sh
```
Preconditions
- For integration and contract tests that touch DB/MQ, ensure infra is up (make up).
- For load/E2E (to be added), ensure services are built and running; provide base URLs via env.

## 📈 Observability workflow
- Each service emits X‑Correlation‑Id and W3C trace headers (traceparent) per request.
- Traces are exported via OpenTelemetry SDKs from each service into Jaeger.
- Explore distributed traces in Jaeger; Prometheus scrapes service metrics; Grafana provides dashboards (import or add via UI).

## 🧭 Directory‑to‑role map
| Role | Start here | Why |
|------|------------|-----|
| New backend dev | docs/architecture/high-level-design.md | Big picture, service responsibilities |
| QA / SDET | docs/testing/test-strategy.md | Coverage strategy, how to run tests |
| DevOps | infra/compose/docker-compose.yml | Local infra services and ports |
| Architects | docs/architecture/service-boundaries.md | Clear bounded contexts and rules |
| New contributor | README.md → docs/architecture/high-level-design.md → docs/testing/test-strategy.md | Guided onboarding path |

## 🗺️ Roadmap
- docs/contracts with OpenAPI per service and AsyncAPI + JSON Schemas for events (planned)
- Service code scaffolds with correlation and error envelope middlewares
- CI workflows for contract validation and container builds
- Compose services and Kubernetes manifests/Helm charts

_Last updated: March 4, 2026_
