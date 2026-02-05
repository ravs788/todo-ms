# Low-Level Design (LLD) - Tag Service

**Version:** 1.0  
**Date:** February 5, 2026  
**Status:** Draft

## Overview

The Tag Service manages tag CRUD operations and handles todo-tag associations. It's built with FastAPI and follows clean architecture principles with SQLAlchemy for data access.

## Directory Structure

```
services/tag-service/
├── app/
│   ├── main.py
│   ├── api/
│   │   └── routes_tags.py
│   ├── core/
│   │   ├── config.py
│   │   ├── security.py
│   │   └── logging.py
│   ├── db/
│   │   ├── session.py
│   │   └── base.py
│   ├── models/
│   │   ├── tag.py
│   │   ├── todo_tag.py
│   │   └── base.py
│   ├── schemas/
│   │   ├── tag.py
│   │   ├── common.py
│   │   └── response.py
│   ├── services/
│   │   ├── tag_service.py
│   │   └── todo_tag_service.py
│   └── deps.py
├── migrations/ (Alembic)
├── tests/
│   ├── unit/
│   └── integration/
└── Dockerfile
```

## Core Models

### Tag Model
- **Fields:** Id, UserId, Name, Color, CreatedAt
- **Relationship:** One-to-Many with TodoTag
- **Constraints:** Name must be unique per user
- **Indexes:** (user_id, name), (user_id, created_at)

### TodoTag Model (Association Table)
- **Fields:** TodoId, TagId, CreatedAt
- **Relationship:** Many-to-One with Todo and Tag
- **Constraints:** Composite primary key (todo_id, tag_id)
- **Purpose:** Many-to-many relationship between Todo and Tag

## Key Endpoints

| Method | Endpoint | Description | Access | Request Body | Response Body |
|--------|----------|-------------|---------|--------------|---------------|
| GET | /api/v1/tags | List tags for user | USER | None | List[TagResponse] |
| POST | /api/v1/tags | Create tag | USER | TagCreate | TagResponse |
| PUT | /api/v1/tags/{id} | Update tag | USER | TagUpdate | TagResponse |
| DELETE | /api/v1/tags/{id} | Delete tag | USER | None | SuccessResponse |
| GET | /api/v1/tags/popular | Popular tags for user | USER | Query params | List[TagResponse] |
| POST | /api/v1/tags/{id}/todos/{todoId} | Associate tag with todo | USER | None | SuccessResponse |
| DELETE | /api/v1/tags/{id}/todos/{todoId} | Disassociate tag from todo | USER | None | SuccessResponse |

### Request/Response Schemas

**TagCreate**
```json
{
  "name": "string",
  "color": "#3498db"
}
```

**TagUpdate**
```json
{
  "name": "string",
  "color": "#3498db"
}
```

**TagResponse**
```json
{
  "id": "uuid",
  "name": "string",
  "color": "#3498db",
  "createdAt": "ISO-8601"
}
```

## Services

### TagService
- **Responsibilities:**
  - Manage tag CRUD operations
  - Enforce ownership constraints
  - Handle tag name uniqueness
- **Key Methods:**
  - `get_tags(user_id)`: List[TagResponse]
  - `create_tag(tag_data, user_id)`: TagResponse
  - `update_tag(tag_id, tag_data, user_id)`: TagResponse
  - `delete_tag(tag_id, user_id)`: bool
  - `get_popular_tags(user_id, limit)`: List[TagResponse]

### TodoTagService
- **Responsibilities:**
  - Manage todo-tag associations
  - Enforce ownership constraints
  - Prevent duplicate associations
- **Key Methods:**
  - `associate_tag_with_todo(tag_id, todo_id, user_id)`: bool
  - `disassociate_tag_from_todo(tag_id, todo_id, user_id)`: bool
  - `get_todo_tags(todo_id, user_id)`: List[UUID]

## Security

### JWT Authentication
- **Validation:** Validate JWT tokens using Auth Service JWKS endpoint
- **Claims:** Extract user ID, roles, and approved status
- **Authorization:** Enforce user ownership of tags

### Authentication Middleware
- **Responsibilities:**
  - Extract and validate JWT token
  - Check approved status
  - Return user ID
- **Key Steps:**
  1. Extract token from Authorization header
  2. Validate token using JWKS endpoint
  3. Check approved claim
  4. Extract user ID

### Authorization
- **User Access:** All endpoints require authenticated user
- **Ownership:** Users can only access their own tags
- **Validation:** Tag and todo ownership verified before operations

## Configuration

### Key Settings (config.py)
```python
class Settings(BaseSettings):
    # API Settings
    api_name: str = "Tag Service"
    api_version: str = "v1"
    
    # Database Settings
    database_url: str = "postgresql+psycopg://postgres:secret@localhost:5432/tag_db"
    
    # Auth Settings
    jwt_jwks_uri: str = "http://auth-service:5001/api/v1/auth/jwks"
    
    # CORS Settings
    cors_origins: list[str] = ["http://localhost:3000"]
    cors_allow_credentials: bool = True
    
    # Redis Settings (for caching)
    redis_url: str = "redis://localhost:6379/0"
```

### Environment Variables
- **DATABASE_URL:** PostgreSQL connection string
- **JWT_JWKS_URI:** Auth service JWKS endpoint URL
- **REDIS_URL:** Redis connection URL

## Error Handling

### Standard Error Response
```json
{
  "code": "HTTP_ERROR",
  "message": "Error message",
  "path": "/api/v1/tags"
}
```

### Custom Exceptions
- **HTTPException:** FastAPI HTTP exceptions
  - 404: Tag not found
  - 409: Tag already exists or already associated
  - 401: Unauthorized
  - 403: Forbidden

### Exception Handlers
- **HTTPExceptionHandler:** Log HTTP exceptions
- **GeneralExceptionHandler:** Log unhandled exceptions

## Testing

### Unit Tests
- **TagService:** Test tag CRUD operations
- **TodoTagService:** Test tag associations
- **Security:** Test JWT validation

### Integration Tests
- **Endpoint Testing:** Test all API endpoints
- **Database Testing:** Test database operations
- **Security Testing:** Test authentication and authorization

## Deployment

### Container Configuration
- **Base Image:** python:3.12-slim
- **Port:** 8001
- **Dependencies:** FastAPI, SQLAlchemy, psycopg2, Alembic

### Docker Compose Configuration
```yaml
services:
  tag-service:
    build: ./services/tag-service
    ports:
      - "8001:8001"
    environment:
      - DATABASE_URL=postgresql+psycopg://postgres:secret@postgres-tag:5432/tag_db
      - JWT_JWKS_URI=http://auth-service:5001/api/v1/auth/jwks
    depends_on:
      - postgres-tag
      - redis
```

### Kubernetes Considerations
- **Replicas:** 2 instances for availability
- **Resources:** Moderate CPU/memory usage
- **Secrets:** Database credentials and JWT keys

## Monitoring

### OpenTelemetry Integration
- **Tracer:** Configure for distributed tracing
- **Exporters:** Jaeger and OTLP
- **Spans:** Track request lifecycle

### Metrics with Prometheus
- **REQUEST_COUNT:** Total number of tag requests
- **REQUEST_DURATION:** Tag request duration
- **TAG_OPERATIONS_TOTAL:** Tag operations count

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-05 | Architecture Team | Initial LLD for Tag Service |
