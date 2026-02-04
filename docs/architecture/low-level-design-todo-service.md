# Low-Level Design (LLD) - Todo Service

**Version:** 1.0  
**Date:** March 2, 2026  
**Status:** Draft

## Overview

The Todo Service manages Todo CRUD operations, business logic, undo/redo functionality, and publishes events for relevant actions. It's built with Spring Boot 3.2 and follows clean architecture principles.

## Project Structure

```
services/todo-service/
├── src/main/java/com/todo/service/
│   ├── controller/
│   │   └── TodoController.java
│   ├── dto/
│   │   ├── TodoRequest.java
│   │   ├── TodoResponse.java
│   │   ├── ReorderRequest.java
│   │   └── PagedResult.java
│   ├── entity/
│   │   ├── Todo.java
│   │   └── TodoHistory.java
│   ├── mapper/
│   │   └── TodoMapper.java (MapStruct)
│   ├── repository/
│   │   ├── TodoRepository.java
│   │   └── TodoHistoryRepository.java
│   ├── security/
│   │   ├── SecurityConfig.java
│   │   ├── JwtAuthConverter.java
│   │   └── UserPrincipal.java
│   ├── service/
│   │   ├── TodoService.java
│   │   ├── EventPublisher.java
│   │   └── UndoRedoService.java
│   ├── config/
│   │   ├── RabbitConfig.java
│   │   ├── OpenApiConfig.java
│   │   └── CacheConfig.java
│   └── TodoServiceApplication.java
├── src/main/resources/
│   ├── application.yml
│   ├── application-dev.yml
│   ├── application-prod.yml
│   └── db/migration/ (Flyway)
├── src/test/java/
│   ├── unit/
│   └── integration/
└── Dockerfile
```

## Core Models

### Todo Entity
- **Fields:** Id, UserId, Title, Description, Completed, Priority (LOW, MEDIUM, HIGH), DueDate, ReminderDate, SortOrder, CreatedAt, UpdatedAt
- **Relationship:** Many-to-One with User
- **Constraints:** Title must not be empty, max 500 characters

### TodoHistory Entity
- **Fields:** Id, TodoId, UserId, Action (CREATE, UPDATE, DELETE, COMPLETE), Snapshot (JSON), CreatedAt
- **Relationship:** Many-to-One with Todo and User
- **Purpose:** Track changes for undo/redo functionality

## Key Endpoints

| Method | Endpoint | Description | Access | Request Body | Response Body |
|--------|----------|-------------|---------|--------------|---------------|
| GET | /api/v1/todos | List todos (filtered, paginated) | USER | Query params | PagedResult<TodoResponse> |
| POST | /api/v1/todos | Create todo | USER | TodoRequest | TodoResponse |
| GET | /api/v1/todos/{id} | Get todo by ID | USER | None | TodoResponse |
| PUT | /api/v1/todos/{id} | Update todo | USER | TodoRequest | TodoResponse |
| DELETE | /api/v1/todos/{id} | Delete todo | USER | None | SuccessResponse |
| PUT | /api/v1/todos/reorder | Batch reorder | USER | ReorderRequest | SuccessResponse |
| POST | /api/v1/todos/{id}/complete | Mark complete | USER | None | TodoResponse |
| GET | /api/v1/todos/history | Undo/redo history | USER | Query params | List<TodoHistory> |

### Request/Response Schemas

**TodoRequest**
```json
{
  "title": "string",
  "description": "string",
  "priority": "LOW|MEDIUM|HIGH",
  "dueDate": "ISO-8601",
  "reminderDate": "ISO-8601"
}
```

**TodoResponse**
```json
{
  "id": "uuid",
  "userId": "uuid",
  "title": "string",
  "description": "string",
  "completed": true,
  "priority": "MEDIUM",
  "dueDate": "ISO-8601",
  "reminderDate": "ISO-8601",
  "sortOrder": 0,
  "createdAt": "ISO-8601",
  "updatedAt": "ISO-8601"
}
```

**PagedResult**
```json
{
  "items": ["TodoResponse"],
  "totalCount": 100,
  "page": 1,
  "size": 10,
  "totalPages": 10
}
```

## Services

### TodoService
- **Responsibilities:**
  - Manage todo CRUD operations
  - Handle filtering, sorting, and pagination
  - Enforce ownership and business rules
  - Maintain todo history for undo/redo
- **Key Methods:**
  - `getTodos(page, size, search, priority, completed)`: PagedResult<TodoResponse>
  - `createTodo(request)`: TodoResponse
  - `updateTodo(id, request)`: TodoResponse
  - `deleteTodo(id)`: void
  - `reorderTodos(request)`: void
  - `completeTodo(id)`: void

### EventPublisher
- **Responsibilities:**
  - Publish todo events to RabbitMQ
  - Handle event serialization and routing
- **Key Events:**
  - `todo.created`: When todo is created or reminder updated
  - `todo.completed`: When todo is marked complete
  - `todo.deleted`: When todo is deleted
- **Key Methods:**
  - `publishTodoCreated(todo)`: void
  - `publishTodoCompleted(todo)`: void
  - `publishTodoDeleted(todo)`: void

### UndoRedoService
- **Responsibilities:**
  - Track todo changes in history
  - Provide undo/redo functionality
- **Key Methods:**
  - `saveHistory(todo, action)`: void
  - `getHistory(todoId)`: List<TodoHistory>

## Security Configuration

### JWT Authentication
- **Validation:** Validate JWT tokens using Auth Service JWKS endpoint
- **Claims:** Extract user ID, roles, and approved status
- **Authorization:** Enforce user ownership of todos

### SecurityConfig
```java
@Configuration
@EnableWebSecurity
@EnableMethodSecurity
public class SecurityConfig {
    @Bean
    public SecurityFilterChain filterChain(HttpSecurity http) throws Exception {
        http
            .csrf(AbstractHttpConfigurer::disable)
            .authorizeHttpRequests(a -> a
                .requestMatchers("/health", "/ready", 
                    "/v3/api-docs/**", 
                    "/swagger-ui/**", 
                    "/swagger-ui.html").permitAll()
                .requestMatchers("/api/v1/todos/**").authenticated()
                .anyRequest().denyAll()
            )
            .oauth2ResourceServer(oauth2 -> oauth2
                .jwt(jwt -> jwt
                    .jwkSetUri(jwksUri)
                    .jwtAuthenticationConverter(jwtAuthConverter())
                )
            );
        return http.build();
    }
}
```

### JwtAuthConverter
- **Responsibilities:**
  - Convert JWT claims to Spring Security authorities
  - Validate approved status
  - Extract user ID and roles
- **Key Logic:**
  - Convert roles to GrantedAuthority
  - Check approved claim
  - Add user ID as custom authority

## Configuration

### Key Settings (application.yml)
```yaml
spring:
  application:
    name: todo-service
  datasource:
    url: jdbc:postgresql://${POSTGRES_HOST}:${POSTGRES_PORT}/todo_db
    username: ${POSTGRES_USER}
    password: ${POSTGRES_PASSWORD}
  jpa:
    hibernate:
      ddl-auto: validate
    properties:
      hibernate:
        dialect: org.hibernate.dialect.PostgreSQLDialect
        format_sql: true
        jdbc:
          batch_size: 20
          order_inserts: true
        order_updates: true
  flyway:
    enabled: true
    locations: classpath:db/migration

security:
  jwt:
    jwks-uri: http://${AUTH_SERVICE_HOST}:5001/api/v1/auth/jwks

rabbitmq:
  host: ${RABBITMQ_HOST}
  port: ${RABBITMQ_PORT}
  username: ${RABBITMQ_USER}
  password: ${RABBITMQ_PASSWORD}
  virtual-host: /
  todo:
    exchange: todo.events
```

### Environment Variables
- **POSTGRES_HOST:** PostgreSQL host
- **POSTGRES_PORT:** PostgreSQL port
- **POSTGRES_USER:** PostgreSQL username
- **POSTGRES_PASSWORD:** PostgreSQL password
- **AUTH_SERVICE_HOST:** Auth service host
- **RABBITMQ_HOST:** RabbitMQ host
- **RABBITMQ_PORT:** RabbitMQ port
- **RABBITMQ_USER:** RabbitMQ username
- **RABBITMQ_PASSWORD:** RabbitMQ password

## Error Handling

### Standard Error Response
```json
{
  "code": "VALIDATION_ERROR",
  "message": "Error message",
  "path": "/api/v1/todos",
  "details": ["Field: Error message"]
}
```

### Custom Exceptions
- **ResourceNotFoundException:** Todo not found
- **ValidationException:** Input validation failed
- **ForbiddenException:** Insufficient permissions
- **BadCredentialsException:** User not approved

### GlobalExceptionHandler
- **Responsibilities:**
  - Map exceptions to standard error responses
  - Handle validation errors
  - Log errors appropriately

## Testing

### Unit Tests
- **TodoService:** Test CRUD operations and business logic
- **EventPublisher:** Test event publishing
- **JwtAuthConverter:** Test token validation

### Integration Tests
- **Endpoint Testing:** Test all API endpoints
- **Database Testing:** Test repository operations
- **Security Testing:** Test authentication and authorization

## Deployment

### Container Configuration
- **Base Image:** eclipse-temurin:17-jre-alpine
- **Port:** 8081
- **Health Checks:** /health and /ready endpoints

### Kubernetes Deployment
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: todo-service
spec:
  replicas: 3
  selector:
    matchLabels:
      app: todo-service
  template:
    metadata:
      labels:
        app: todo-service
    spec:
      containers:
      - name: todo-service
        image: todo-service:latest
        ports:
        - containerPort: 8081
        env:
        - name: POSTGRES_HOST
          value: postgres-todo
        - name: RABBITMQ_HOST
          value: rabbitmq
        livenessProbe:
          httpGet:
            path: /health
            port: 8081
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /ready
            port: 8081
          initialDelaySeconds: 5
          periodSeconds: 5
```

## Monitoring

### Metrics
- `todo_requests_total`: Total API requests
- `todo_operations_duration_ms`: Operation execution time
- `active_todos_count`: Current active todos count
- `todo_completions_total`: Total todo completions

### Health Checks
- **Liveness:** /health endpoint
- **Readiness:** /ready endpoint (includes database connectivity check)

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-02 | Architecture Team | Initial LLD for Todo Service |
