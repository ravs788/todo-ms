# Test Strategy - Todo Microservices Solution

**Version:** 1.0  
**Date:** February 5, 2026  
**Status:** Approved

---

## Table of Contents

1. [Overview](#overview)
2. [Testing Principles](#testing-principles)
3. [Test Pyramid](#test-pyramid)
4. [Testing Types](#testing-types)
5. [Test Environments](#test-environments)
6. [CI/CD Integration](#cicd-integration)
7. [Test Data Management](#test-data-management)
8. [Coverage Targets](#coverage-targets)
9. [Appendix](#appendix)

---

## Overview

This document outlines the comprehensive testing strategy for the Todo Microservices Solution. The strategy covers unit, integration, contract, end-to-end, load, security, and chaos engineering testing across all 7 microservices.

### Goals

1. **Ensure Quality**: Maintain 80%+ test coverage across all services
2. **Fast Feedback**: Unit tests run in < 5 minutes, integration tests < 15 minutes
3. **Confidence**: E2E tests cover critical user journeys
4. **Performance**: Load tests validate SLAs (p95 < 500ms)
5. **Security**: Automated security scanning in CI pipeline
6. **Reliability**: Chaos tests validate fault tolerance

---

## Testing Principles

### 1. Shift Left
Test early and often. Unit tests written alongside production code.

### 2. Test Independence
Tests should not depend on execution order or external state.

### 3. Deterministic Tests
Same input always produces same output. No flaky tests.

### 4. Fast Execution
Unit tests run in milliseconds, integration tests in seconds.

### 5. Meaningful Coverage
Focus on business logic, not framework code. 80% is a target, not a mandate.

### 6. Test Data Isolation
Each test creates and cleans up its own data.

---

## Test Pyramid

```
                    ▲
                   ╱ ╲
                  ╱   ╲
                 ╱ E2E ╲          ~10 tests (Critical user journeys)
                ╱───────╲         Selenium/Playwright
               ╱         ╲        Slow, expensive, fragile
              ╱───────────╲
             ╱             ╲
            ╱  Integration  ╲     ~100 tests (Service boundaries)
           ╱─────────────────╲    Testcontainers, WireMock
          ╱                   ╲   Medium speed, moderate cost
         ╱─────────────────────╲
        ╱                       ╲
       ╱         Unit            ╲  ~1000 tests (Business logic)
      ╱───────────────────────────╲  xUnit/JUnit/pytest
     ╱                             ╲ Fast, cheap, reliable
    ╱───────────────────────────────╲
   ────────────────────────────────────
```

**Distribution:**
- Unit Tests: 70%
- Integration Tests: 20%
- Contract Tests: 5%
- E2E Tests: 5%

---

## Testing Types

### 1. Unit Testing

**Scope:** Individual classes, functions, methods

**Goal:** Validate business logic in isolation

**Tools by Service:**
- **C# Services**: xUnit + Moq + FluentAssertions
- **Java Services**: JUnit 5 + Mockito + AssertJ
- **Python Services**: pytest + pytest-mock + pytest-cov

#### Example: Authentication Service (C#)

```csharp
// TokenServiceTests.cs
public class TokenServiceTests
{
    private readonly Mock<IKeyProvider> _keyProviderMock;
    private readonly TokenService _sut;
    
    public TokenServiceTests()
    {
        _keyProviderMock = new Mock<IKeyProvider>();
        _sut = new TokenService(_keyProviderMock.Object);
    }
    
    [Fact]
    public async Task GenerateTokens_WithValidUser_ReturnsTokens()
    {
        // Arrange
        var user = new User 
        { 
            Id = Guid.NewGuid(), 
            Username = "testuser",
            Roles = new[] { "USER" },
            IsApproved = true
        };
        
        _keyProviderMock
            .Setup(x => x.GetPrivateKey())
            .Returns(TestKeys.PrivateKey);
        
        // Act
        var result = await _sut.GenerateTokens(user);
        
        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.ExpiresIn.Should().Be(900);
    }
    
    [Fact]
    public async Task ValidateToken_WithExpiredToken_ThrowsSecurityException()
    {
        // Arrange
        var expiredToken = GenerateExpiredToken();
        
        // Act & Assert
        await Assert.ThrowsAsync<SecurityTokenExpiredException>(
            () => _sut.ValidateToken(expiredToken)
        );
    }
}
```

#### Example: Todo Service (Java)

```java
// TodoServiceTest.java
@ExtendWith(MockitoExtension.class)
class TodoServiceTest {
    
    @Mock
    private TodoRepository repository;
    
    @Mock
    private EventPublisher eventPublisher;
    
    @InjectMocks
    private TodoService service;
    
    @Test
    void createTodo_withValidData_savesAndPublishesEvent() {
        // Arrange
        var request = new TodoRequest("Buy groceries", "Milk, bread, eggs");
        var userId = UUID.randomUUID();
        
        var savedTodo = Todo.builder()
            .id(UUID.randomUUID())
            .title(request.getTitle())
            .userId(userId)
            .build();
            
        when(repository.save(any(Todo.class))).thenReturn(savedTodo);
        
        // Act
        var result = service.createTodo(request, userId.toString());
        
        // Assert
        assertThat(result).isNotNull();
        assertThat(result.getTitle()).isEqualTo("Buy groceries");
        
        verify(repository).save(any(Todo.class));
        verify(eventPublisher).publish(eq("todo.created"), any(TodoCreatedEvent.class));
    }
}
```

#### Example: Tag Service (Python)

```python
# test_tag_service.py
import pytest
from unittest.mock import Mock
from app.services.tag_service import TagService
from app.models.tag import Tag

@pytest.fixture
def mock_db():
    return Mock()

@pytest.fixture
def tag_service(mock_db):
    return TagService(mock_db)

def test_create_tag_success(tag_service, mock_db):
    # Arrange
    tag_data = TagCreate(name="Work", color="#FF5733")
    user_id = "user-123"
    
    expected_tag = Tag(
        id="tag-123",
        name="Work",
        color="#FF5733",
        user_id=user_id
    )
    mock_db.add = Mock()
    mock_db.commit = Mock()
    mock_db.refresh = Mock(side_effect=lambda t: setattr(t, 'id', 'tag-123'))
    
    # Act
    result = tag_service.create_tag(tag_data, user_id)
    
    # Assert
    assert result.name == "Work"
    assert result.color == "#FF5733"
    mock_db.add.assert_called_once()
    mock_db.commit.assert_called_once()

def test_create_duplicate_tag_raises_error(tag_service, mock_db):
    # Arrange
    tag_data = TagCreate(name="Work", color="#FF5733")
    mock_db.add = Mock(side_effect=IntegrityError("Duplicate", None, None))
    
    # Act & Assert
    with pytest.raises(DuplicateTagError):
        tag_service.create_tag(tag_data, "user-123")
```

**Unit Test Checklist:**
- [ ] Test happy paths
- [ ] Test edge cases and boundary conditions
- [ ] Test error handling
- [ ] Mock external dependencies
- [ ] Use descriptive test names
- [ ] Follow AAA pattern (Arrange, Act, Assert)

---

### 2. Integration Testing

**Scope:** Service interactions with databases, message queues, and external services

**Goal:** Validate component integration

**Tools:** Testcontainers (Docker containers for testing)

#### Example: Todo Service Database Integration

```java
// TodoRepositoryIntegrationTest.java
@DataJpaTest
@Testcontainers
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
class TodoRepositoryIntegrationTest {
    
    @Container
    static PostgreSQLContainer<?> postgres = new PostgreSQLContainer<>("postgres:16-alpine")
        .withDatabaseName("todo_test")
        .withUsername("test")
        .withPassword("test");
    
    @DynamicPropertySource
    static void configureProperties(DynamicPropertyRegistry registry) {
        registry.add("spring.datasource.url", postgres::getJdbcUrl);
        registry.add("spring.datasource.username", postgres::getUsername);
        registry.add("spring.datasource.password", postgres::getPassword);
    }
    
    @Autowired
    private TodoRepository repository;
    
    @Test
    void findByUserId_returnsUserTodos() {
        // Arrange
        var userId = UUID.randomUUID();
        var todo1 = Todo.builder().title("Task 1").userId(userId).build();
        var todo2 = Todo.builder().title("Task 2").userId(userId).build();
        
        repository.saveAll(List.of(todo1, todo2));
        
        // Act
        var result = repository.findByUserId(userId);
        
        // Assert
        assertThat(result).hasSize(2);
        assertThat(result).extracting(Todo::getTitle)
            .containsExactlyInAnyOrder("Task 1", "Task 2");
    }
}
```

#### Example: Notification Service RabbitMQ Integration

```python
# test_notification_consumer.py
import pytest
from testcontainers.rabbitmq import RabbitMqContainer
from app.consumers.notification_consumer import NotificationConsumer

@pytest.fixture(scope="module")
def rabbitmq_container():
    with RabbitMqContainer("rabbitmq:3.12-management-alpine") as container:
        yield container

def test_consume_todo_created_event(rabbitmq_container):
    # Arrange
    connection = pika.BlockingConnection(
        pika.ConnectionParameters(host=rabbitmq_container.get_container_host_ip(),
                                 port=rabbitmq_container.get_exposed_port(5672))
    )
    channel = connection.channel()
    
    consumer = NotificationConsumer(channel)
    
    # Act
    event = TodoCreatedEvent(
        todo_id="todo-123",
        user_id="user-123",
        title="Test Todo",
        reminder_date="2026-03-10T10:00:00Z"
    )
    
    channel.basic_publish(
        exchange='todo.events',
        routing_key='todo.created',
        body=json.dumps(event.dict())
    )
    
    # Assert
    # Wait for consumer to process
    time.sleep(1)
    
    # Verify reminder scheduled
    scheduled_reminders = get_scheduled_reminders()
    assert len(scheduled_reminders) == 1
    assert scheduled_reminders[0].todo_id == "todo-123"
```

---

### 3. Contract Testing

**Scope:** API contracts between services

**Goal:** Ensure consumer expectations match provider implementations

**Tool:** Pact (Consumer-Driven Contracts)

#### Example: Frontend → Todo Service Contract

**Consumer Test (Frontend):**

```javascript
// todoServiceContract.test.js
const { Pact } = require('@pact-foundation/pact');
const { getTodos } = require('./todoService');

describe('Todo Service Contract', () => {
  const provider = new Pact({
    consumer: 'TodoFrontend',
    provider: 'TodoService',
    port: 8080,
  });

  beforeAll(() => provider.setup());
  afterAll(() => provider.finalize());

  describe('GET /api/v1/todos', () => {
    beforeAll(() => {
      return provider.addInteraction({
        state: 'user has todos',
        uponReceiving: 'a request for todos',
        withRequest: {
          method: 'GET',
          path: '/api/v1/todos',
          headers: {
            Authorization: Matchers.regex({
              generate: 'Bearer token123',
              matcher: '^Bearer .+$'
            })
          },
          query: { page: '1', size: '10' }
        },
        willRespondWith: {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
          body: Matchers.eachLike({
            id: Matchers.uuid(),
            title: Matchers.string('Buy groceries'),
            completed: Matchers.boolean(false),
            createdAt: Matchers.iso8601DateTime()
          })
        }
      });
    });

    it('returns a list of todos', async () => {
      const todos = await getTodos({ page: 1, size: 10 });
      
      expect(todos).toBeDefined();
      expect(todos.length).toBeGreaterThan(0);
      expect(todos[0]).toHaveProperty('id');
      expect(todos[0]).toHaveProperty('title');
    });
  });
});
```

**Provider Verification (Todo Service):**

```java
// TodoServiceContractTest.java
@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)
@Provider("TodoService")
@PactFolder("pacts")
public class TodoServiceContractTest {
    
    @LocalServerPort
    private int port;
    
    @TestTemplate
    @ExtendWith(PactVerificationInvocationContextProvider.class)
    void pactVerificationTestTemplate(PactVerificationContext context) {
        context.verifyInteraction();
    }
    
    @BeforeEach
    void before(PactVerificationContext context) {
        context.setTarget(new HttpTestTarget("localhost", port));
    }
    
    @State("user has todos")
    void userHasTodos() {
        // Setup test data
        var userId = UUID.fromString("user-123");
        todoRepository.save(Todo.builder()
            .title("Buy groceries")
            .userId(userId)
            .build());
    }
}
```

---

### 4. End-to-End (E2E) Testing

**Scope:** Complete user journeys across all services

**Goal:** Validate critical business flows

**Tool:** Playwright (existing test suite adapted)

#### Test Scenarios

1. **User Registration & Approval Flow**
   - User registers → Status: PENDING
   - Admin logs in → Approves user
   - User logs in → Success

2. **Todo CRUD Flow**
   - User logs in
   - Creates todo
   - Updates todo
   - Deletes todo

3. **Tag Management Flow**
   - Create tag
   - Assign to todo
   - Filter todos by tag

4. **Notification Flow**
   - Enable notifications
   - Create todo with reminder
   - Verify notification sent

#### Example: Playwright Test

```typescript
// tests/e2e/todo-crud.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Todo CRUD Operations', () => {
  test.beforeEach(async ({ page }) => {
    // Login
    await page.goto('http://localhost:3000/login');
    await page.fill('[data-testid="username"]', 'testuser');
    await page.fill('[data-testid="password"]', 'password123');
    await page.click('[data-testid="login-button"]');
    
    await expect(page).toHaveURL('http://localhost:3000/todos');
  });

  test('should create a new todo', async ({ page }) => {
    // Click create button
    await page.click('[data-testid="create-todo-button"]');
    
    // Fill form
    await page.fill('[data-testid="todo-title"]', 'Buy groceries');
    await page.fill('[data-testid="todo-description"]', 'Milk, bread, eggs');
    await page.selectOption('[data-testid="todo-priority"]', 'HIGH');
    
    // Submit
    await page.click('[data-testid="submit-todo"]');
    
    // Verify todo appears in list
    await expect(page.locator('[data-testid="todo-item"]')).toContainText('Buy groceries');
  });

  test('should update an existing todo', async ({ page }) => {
    // Click first todo
    await page.click('[data-testid="todo-item"]:first-child');
    
    // Click edit button
    await page.click('[data-testid="edit-todo-button"]');
    
    // Update title
    await page.fill('[data-testid="todo-title"]', 'Buy groceries - UPDATED');
    
    // Submit
    await page.click('[data-testid="submit-todo"]');
    
    // Verify update
    await expect(page.locator('[data-testid="todo-item"]')).toContainText('UPDATED');
  });
});
```

---

### 5. Load Testing

**Scope:** Performance under load

**Goal:** Validate SLAs and identify bottlenecks

**Tool:** k6 (Grafana)

#### Test Scenarios

1. **Login Load Test**
   - 100 VUs
   - 10 requests/second
   - Duration: 5 minutes
   - Success criteria: p95 < 500ms, error rate < 1%

2. **Todo List Load Test**
   - 500 VUs
   - 50 requests/second
   - Duration: 10 minutes
   - Success criteria: p95 < 1s, error rate < 0.5%

#### Example: k6 Script

```javascript
// load-tests/login-load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  stages: [
    { duration: '2m', target: 50 },   // Ramp up
    { duration: '5m', target: 100 },  // Stay at 100 VUs
    { duration: '2m', target: 0 },    // Ramp down
  ],
  thresholds: {
    'http_req_duration': ['p(95)<500'],
    'http_req_failed': ['rate<0.01'],
  },
};

export default function () {
  const url = 'http://localhost:8000/api/v1/auth/login';
  const payload = JSON.stringify({
    username: `user${__VU}`,
    password: 'password123',
  });
  
  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
  };
  
  const response = http.post(url, payload, params);
  
  check(response, {
    'login successful': (r) => r.status === 200,
    'token received': (r) => r.json('accessToken') !== undefined,
    'response time OK': (r) => r.timings.duration < 500,
  });
  
  sleep(1);
}
```

---

### 6. Security Testing

**Scope:** Vulnerability scanning and penetration testing

**Goal:** Identify and fix security issues

**Tools:**
- **OWASP ZAP**: API security scanning
- **SonarQube**: Static code analysis
- **Trivy**: Container image scanning
- **Snyk**: Dependency vulnerability scanning

#### Test Cases

1. **JWT Validation**
   - Expired token → 401
   - Tampered token → 401
   - Missing token → 401
   - Invalid signature → 401

2. **SQL Injection**
   - Test input fields with SQL payloads
   - Verify parameterized queries prevent injection

3. **XSS (Cross-Site Scripting)**
   - Test input fields with XSS payloads
   - Verify output encoding

4. **Rate Limiting**
   - Send 1000 requests/second
   - Verify rate limiter blocks after threshold

#### Example: Security Test

```bash
# OWASP ZAP API Scan
docker run -v $(pwd):/zap/wrk/:rw \
  -t owasp/zap2docker-stable zap-api-scan.py \
  -t http://localhost:8000/api/v1 \
  -f openapi \
  -r zap-report.html
```

---

### 7. Chaos Engineering

**Scope:** Fault tolerance and resilience

**Goal:** Validate system behavior under failure conditions

**Tool:** Chaos Mesh (Kubernetes)

#### Test Scenarios

1. **Network Latency**
   - Inject 500ms latency between Todo Service and PostgreSQL
   - Verify graceful degradation

2. **Pod Failures**
   - Kill Tag Service pod randomly
   - Verify Kubernetes restarts and requests succeed

3. **Database Connection Exhaustion**
   - Simulate max connections reached
   - Verify circuit breaker activates

#### Example: Chaos Mesh Experiment

```yaml
# chaos-experiments/pod-failure.yaml
apiVersion: chaos-mesh.org/v1alpha1
kind: PodChaos
metadata:
  name: tag-service-failure
  namespace: todo-app
spec:
  action: pod-failure
  mode: one
  selector:
    namespaces:
      - todo-app
    labelSelectors:
      app: tag-service
  duration: '30s'
  scheduler:
    cron: '@every 10m'
```

---

## Test Environments

| Environment | Purpose | Data | Deployment |
|-------------|---------|------|------------|
| **Local** | Developer testing | Mock/seed data | Docker Compose |
| **CI** | Automated testing | Testcontainers | GitHub Actions |
| **Dev** | Integration testing | Synthetic data | Kubernetes (Dev) |
| **Staging** | Pre-production testing | Anonymized prod data | Kubernetes (Staging) |
| **Production** | Monitoring only | Real data | Kubernetes (Prod) |

---

## CI/CD Integration

### GitHub Actions Pipeline

```yaml
# .github/workflows/ci-cd.yml
name: CI/CD Pipeline

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        service: [auth-service, user-service, todo-service, tag-service, notification-service, admin-service]
    steps:
      - uses: actions/checkout@v4
      - name: Run unit tests
        run: |
          cd services/${{ matrix.service }}
          make test-unit
      - name: Upload coverage
        uses: codecov/codecov-action@v3

  integration-tests:
    needs: unit-tests
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Start testcontainers
        run: docker compose -f docker-compose.test.yml up -d
      - name: Run integration tests
        run: make test-integration

  contract-tests:
    needs: integration-tests
    runs-on: ubuntu-latest
    steps:
      - name: Run Pact consumer tests
        run: cd tests-contract && npm run test:consumer
      - name: Publish pacts
        run: npm run pact:publish

  e2e-tests:
    needs: contract-tests
    runs-on: ubuntu-latest
    steps:
      - name: Start all services
        run: docker compose up -d
      - name: Wait for services
        run: ./scripts/wait-for-services.sh
      - name: Run Playwright tests
        run: cd tests-e2e && npm test
      - name: Upload test results
        uses: actions/upload-artifact@v3
        with:
          name: playwright-report
          path: tests-e2e/playwright-report/

  load-tests:
    if: github.ref == 'refs/heads/main'
    needs: e2e-tests
    runs-on: ubuntu-latest
    steps:
      - name: Run k6 load tests
        run: k6 run tests-load/login-load-test.js

  security-scan:
    needs: unit-tests
    runs-on: ubuntu-latest
    steps:
      - name: Run Trivy scan
        run: trivy image todo-service:latest
      - name: Run Snyk scan
        run: snyk test
```

---

## Test Data Management

### Strategies

1. **Test Fixtures**: Predefined test data in code
2. **Factories**: Generate test data dynamically
3. **Seed Scripts**: Load test data into databases
4. **Anonymization**: Use anonymized production data in staging

### Example: Test Data Factory (Java)

```java
// TodoTestDataFactory.java
public class TodoTestDataFactory {
    
    public static Todo createTodo() {
        return Todo.builder()
            .id(UUID.randomUUID())
            .title("Test Todo")
            .description("Test Description")
            .userId(UUID.randomUUID())
            .completed(false)
            .priority(Priority.MEDIUM)
            .createdAt(Instant.now())
            .build();
    }
    
    public static Todo createTodoWithTitle(String title) {
        return createTodo().toBuilder()
            .title(title)
            .build();
    }
    
    public static List<Todo> createTodos(int count) {
        return IntStream.range(0, count)
            .mapToObj(i -> createTodoWithTitle("Todo " + i))
            .collect(Collectors.toList());
    }
}
```

---

## Coverage Targets

| Service | Unit | Integration | E2E | Total |
|---------|------|-------------|-----|-------|
| Auth Service | 85% | 70% | 90% | 80% |
| User Service | 85% | 70% | 85% | 80% |
| Todo Service | 85% | 75% | 95% | 85% |
| Tag Service | 80% | 70% | 80% | 75% |
| Notification Service | 80% | 65% | 85% | 75% |
| Admin Service | 85% | 70% | 90% | 80% |

**Overall Target:** 80%+ across all services

---

## Appendix

### A. Test Naming Conventions

**Pattern:** `MethodName_StateUnderTest_ExpectedBehavior`

Examples:
- `createTodo_withValidData_returnsTodo`
- `validateToken_withExpiredToken_throwsException`
- `getUserProfile_whenUserNotFound_returns404`

### B. Test Organization

```
service/
├── src/
│   ├── main/
│   └── test/
│       ├── unit/
│       │   └── services/
│       │       └── TodoServiceTest.java
│       ├── integration/
│       │   └── repositories/
│       │       └── TodoRepositoryIntegrationTest.java
│       └── resources/
│           └── test-data.json
```

### C. Continuous Improvement

- **Weekly**: Review test failures and flaky tests
- **Monthly**: Analyze coverage trends
- **Quarterly**: Review and update test strategy

---

**Document Version:** 1.0  
**Last Updated:** February 5, 2026  
**Next Review:** June 2, 2026
