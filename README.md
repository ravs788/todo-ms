# Todo Microservices Solution

[![Architecture](https://img.shields.io/badge/Architecture-Microservices-blue)](docs/architecture/)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker)](infrastructure/docker/)
[![Kubernetes](https://img.shields.io/badge/Kubernetes-Ready-326CE5?logo=kubernetes)](infrastructure/kubernetes/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A polyglot microservices architecture implementation of the Todo application using C#, Java, Python, and React.

---

## 🎯 Overview

This project demonstrates a **granular microservices architecture** migrated from a monolithic Spring Boot application. The solution leverages multiple technology stacks to showcase best practices in distributed systems design, deployment, and testing.

### Key Features

- **Polyglot Architecture**: Services built with C# (.NET 8), Java (Spring Boot 3.x), and Python (FastAPI)
- **Containerized**: All services packaged as Docker containers
- **Kubernetes-Ready**: Complete K8s manifests and Helm charts
- **Event-Driven**: Asynchronous communication via RabbitMQ
- **Observable**: Distributed tracing (Jaeger), metrics (Prometheus/Grafana), and centralized logging (ELK)
- **Secure**: JWT-based authentication with JWKS, API Gateway with rate limiting
- **Tested**: Comprehensive test strategy covering unit, integration, contract, E2E, load, and security testing

---

## 🏗️ Architecture

### System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         React Frontend                           │
│                    (Port 3000 - Nginx)                          │
└────────────────────────────┬────────────────────────────────────┘
                             │ HTTPS/REST
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Kong API Gateway                            │
│            (Port 8000) - Rate Limiting, CORS, Auth              │
└─────┬────────┬─────────┬─────────┬─────────┬─────────┬─────────┘
      │ /auth  │ /users  │ /todos  │ /tags   │ /notify │ /admin
      │        │         │         │         │         │
      ▼        ▼         ▼         ▼         ▼         ▼
┌─────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌──────────┐ ┌───────┐
│  Auth   │ │  User  │ │  Todo  │ │  Tag   │ │ Notify   │ │ Admin │
│ Service │ │ Service│ │ Service│ │ Service│ │ Service  │ │Service│
│  C#     │ │  C#    │ │  Java  │ │ Python │ │ Python   │ │ Java  │
│ .NET 8  │ │ .NET 8 │ │ Spring │ │FastAPI │ │ FastAPI  │ │Spring │
│:5001    │ │:5002   │ │:8081   │ │:8001   │ │:8002     │ │:8082  │
└─────────┘ └────────┘ └────────┘ └────────┘ └──────────┘ └───────┘
```

### Service Inventory

| Service | Language/Framework | Port | Purpose | Database |
|---------|-------------------|------|---------|----------|
| **Authentication Service** | C# (.NET 8) | 5001 | JWT/OAuth, token management | PostgreSQL |
| **User Management Service** | C# (.NET 8) | 5002 | User CRUD, profiles | PostgreSQL |
| **Todo Service** | Java (Spring Boot 3.x) | 8081 | Todo CRUD, business logic | PostgreSQL |
| **Tag Service** | Python (FastAPI) | 8001 | Tag management | PostgreSQL |
| **Notification Service** | Python (FastAPI) | 8002 | Push notifications, reminders | PostgreSQL + Redis |
| **Admin Service** | Java (Spring Boot 3.x) | 8082 | User approval, admin ops | PostgreSQL |
| **API Gateway** | Kong | 8000 | Routing, rate limiting | Redis |

### Technology Stack

#### Backend Services
- **C# Services**: .NET 8, Entity Framework Core, IdentityServer4/Custom JWT
- **Java Services**: Spring Boot 3.2, Spring Data JPA, Spring Security, Spring Cloud Stream
- **Python Services**: FastAPI 0.110, SQLAlchemy 2.0, Celery, PyWebPush

#### Infrastructure
- **API Gateway**: Kong with plugins
- **Message Broker**: RabbitMQ
- **Databases**: PostgreSQL 16 (per service), Redis (caching/sessions)
- **Observability**: Jaeger (tracing), Prometheus + Grafana (metrics), ELK Stack (logging)
- **Orchestration**: Kubernetes with Helm
- **Containerization**: Docker + Docker Compose

#### Frontend
- **Framework**: React 19
- **Build Tool**: Webpack
- **State Management**: Context API
- **UI**: Bootstrap 5

---

## 🚀 Quick Start

### Prerequisites

- Docker 24+
- Docker Compose 2.20+
- Kubernetes (Minikube/Kind/Docker Desktop) - Optional
- kubectl 1.28+ - Optional
- Helm 3.12+ - Optional

### Local Development (Docker Compose)

```bash
# Clone the repository
cd todo-ms

# Start all services
docker compose up -d

# Wait for services to be healthy
./scripts/wait-for-services.sh

# Access the application
# Frontend: http://localhost:3000
# API Gateway: http://localhost:8000
# Jaeger UI: http://localhost:16686
# Grafana: http://localhost:3001
```

### Kubernetes Deployment

```bash
# Create namespace
kubectl create namespace todo-app

# Deploy using Helm
helm install todo-app infrastructure/kubernetes/helm/todo-app \
  --namespace todo-app

# Port-forward to access locally
kubectl port-forward -n todo-app svc/api-gateway 8000:8000
kubectl port-forward -n todo-app svc/frontend 3000:80
```

---

## 📁 Project Structure

```
todo-ms/
├── services/                      # Microservices
│   ├── auth-service/             # C# Authentication Service
│   ├── user-service/             # C# User Management Service
│   ├── todo-service/             # Java Todo Service
│   ├── tag-service/              # Python Tag Service
│   ├── notification-service/     # Python Notification Service
│   └── admin-service/            # Java Admin Service
│
├── gateway/                      # Kong API Gateway configuration
│
├── frontend/                     # React frontend application
│
├── infrastructure/               # Infrastructure as Code
│   ├── docker/                   # Docker Compose files
│   ├── kubernetes/               # K8s manifests and Helm charts
│   ├── observability/            # Monitoring and tracing configs
│   └── rabbitmq/                 # Message broker configuration
│
├── tests-e2e/                    # End-to-end Playwright tests
├── tests-load/                   # k6 load tests
├── tests-contract/               # Pact contract tests
│
├── docs/                         # Documentation
│   ├── architecture/             # Architecture decisions and designs
│   ├── testing/                  # Testing strategies and guides
│   └── deployment/               # Deployment and runbooks
│
└── scripts/                      # Utility scripts
```

---

## 📚 Documentation

### Architecture
- [Architectural Decision Records (ADRs)](docs/architecture/ADR-001-microservices-architecture.md)
- [High-Level Design (HLD)](docs/architecture/high-level-design.md)
- [Low-Level Design (LLD)](docs/architecture/low-level-design.md)
- [Service Boundaries](docs/architecture/service-boundaries.md)
- [Sequence Diagrams](docs/architecture/sequence-diagrams.md)

### Testing
- [Test Strategy](docs/testing/test-strategy.md)
- [Unit Testing Guide](docs/testing/unit-testing-guide.md)
- [Integration Testing Guide](docs/testing/integration-testing-guide.md)
- [E2E Testing Guide](docs/testing/e2e-testing-guide.md)
- [Load Testing Guide](docs/testing/load-testing-guide.md)

### Deployment
- [Docker Setup](docs/deployment/docker-setup.md)
- [Kubernetes Setup](docs/deployment/kubernetes-setup.md)
- [Migration Plan](docs/deployment/migration-plan.md)

---

## 🧪 Testing

### Run All Tests

```bash
# Unit tests (all services)
make test-unit

# Integration tests
make test-integration

# Contract tests
make test-contract

# E2E tests
cd tests-e2e && npm test

# Load tests
make test-load
```

### Test Coverage

- **Unit Tests**: 80%+ coverage across all services
- **Integration Tests**: Database, message queue, and inter-service communication
- **Contract Tests**: Consumer-driven contracts with Pact
- **E2E Tests**: Critical user journeys with Playwright
- **Load Tests**: Performance benchmarks with k6

---

## 🔒 Security

- **Authentication**: JWT with RS256 asymmetric signing
- **Authorization**: Role-based access control (RBAC)
- **API Gateway**: Rate limiting, CORS, request validation
- **Secrets Management**: Kubernetes secrets / Azure Key Vault
- **Network Policies**: Service-to-service encryption with mTLS (optional)

---

## 📊 Observability

### Distributed Tracing (Jaeger)
- Trace requests across all services
- View service dependencies and latency
- Access: http://localhost:16686

### Metrics (Prometheus + Grafana)
- Service health and performance metrics
- Custom business metrics
- Pre-built dashboards for each service
- Access: http://localhost:3001

### Centralized Logging (ELK Stack)
- Aggregated logs from all services
- Search and filter by service, trace ID, user ID
- Access: http://localhost:5601

---

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- Inspired by microservices patterns from Martin Fowler and Sam Newman
- Technology choices guided by production-grade examples from Microsoft, Spring, and Python communities

---

**Last Updated**: March 2, 2026
