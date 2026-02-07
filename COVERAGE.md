# Test & Code Coverage Overview

Purpose: Single root document that summarizes test and code coverage across layers and services. Each service/language may keep its own detailed reports (in service-local folders), but this file aggregates key metrics.

Last updated: 2026-02-08 (lines: 96.0%, branches: 81.5% for Auth Service)

## Summary (Current Baseline)

- Scope: Local thin slice for Auth Service with initial unit/integration tests
- Metrics below will be auto-updated once per run when coverage tooling is wired (see “How to generate coverage”)

### By Service

| Service            | Unit Tests (pass) | Integration Tests (pass) | Contract | E2E | Code Coverage | Notes |
|--------------------|-------------------:|--------------------------:|---------:|----:|--------------:|-------|
| Auth Service (.NET)| 8                  | 14                        | 0        | 0   | 96.0% lines / 81.5% branches | Positive + negative paths for Register/Login/Refresh/Logout/JWKS/Health (TestServer + InMemory) |
| User Service (.NET)| 0                  | 0                         | 0        | 0   | TBD (%)       | Planned |
| Todo Service (Java)| 0                  | 0                         | 0        | 0   | TBD (%)       | Planned |
| Tag Service (Py)   | 0                  | 0                         | 0        | 0   | TBD (%)       | Planned |
| Notification (Py)  | 0                  | 0                         | 0        | 0   | TBD (%)       | Planned |
| Admin (Java)       | 0                  | 0                         | 0        | 0   | TBD (%)       | Planned |
| Gateway (Kong)     | n/a                | n/a                       | n/a      | n/a | n/a           | Declarative config; linting rules TBD |

### By Test Layer

| Layer         | Definition                                           | Current Status |
|---------------|------------------------------------------------------|----------------|
| Unit          | Service-internal logic (fast, isolated)              | Auth: 8 passing |
| Integration   | In-process app endpoints, TestServer/Testcontainers  | Auth: 14 passing |
| Contract      | Provider/consumer Pact tests                         | Planned |
| E2E           | Cross-service user flows (Playwright)                | Planned |
| Load/Sec/Chaos| k6, ZAP, Trivy/Snyk, Chaos Mesh                      | Planned |

## How to generate coverage (per tech)

This section standardizes how each service emits coverage. Local reports live within each service; CI can later publish artifacts and update this root doc.

### .NET (xUnit) – e.g., Auth Service

Option A: Built-in collector (Cobertura)

- Add to test project (already present): Microsoft.NET.Test.Sdk
- Run:
  - dotnet test services/auth-service/tests/AuthService.Tests/AuthService.Tests.csproj --collect "XPlat Code Coverage"
- Output:
  - TestResults/<guid>/coverage.cobertura.xml

Option B: Coverlet MSBuild (fine-grained control)

- Add to test project:
  - <PackageReference Include="coverlet.collector" Version="3.2.0" />
- Run:
  - dotnet test -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura -p:CoverletOutput=TestResults/coverage/
- Output:
  - services/auth-service/tests/AuthService.Tests/TestResults/coverage/coverage.cobertura.xml

### Java (JUnit) – e.g., Todo/Admin

- Add JaCoCo plugin to pom.xml and run:
  - mvn -q test
- Output:
  - target/site/jacoco/jacoco.xml

### Python (pytest)

- Install:
  - pip install pytest pytest-cov
- Run:
  - pytest --cov=app --cov-report=xml:coverage.xml
- Output:
  - coverage.xml

## Consolidation workflow (proposed)

1) Generate service-local coverage report (Cobertura/Jacoco/pytest-cov XML).
2) Store under each service:
   - .NET: services/<name>/tests/<TestProject>/TestResults/**/coverage.cobertura.xml
   - Java: services/<name>/target/site/jacoco/jacoco.xml
   - Python: services/<name>/coverage.xml
3) Summarize into this doc:
   - A script (to be added under scripts/coverage/summarize.py) parses XMLs, computes line/branch coverage, and updates the tables above.
   - CI step runs coverage, uploads artifacts, and commits doc update (optional).

## Next actions

- [ ] Add coverlet.collector to Auth test project for stable Cobertura XML output
- [ ] Add coverage Makefile targets:
  - make test-coverage-auth (dotnet)
  - make test-coverage-todo (mvn)
  - make test-coverage-tag (pytest)
- [ ] Add scripts/coverage/summarize.py to merge coverage across services
- [ ] Wire CI job to generate coverage on PR and update this doc

## Retention

- Service-local coverage artifacts may be gitignored to avoid noise.
- CI stores artifacts per run; this doc tracks only summarized numbers and links if desired.
