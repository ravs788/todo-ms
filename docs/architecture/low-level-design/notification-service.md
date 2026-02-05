# Low-Level Design (LLD) - Notification Service

**Version:** 1.0  
**Date:** March 2, 2026  
**Status:** Draft

## Overview

The Notification Service handles push notifications, reminder scheduling, and notification history tracking. It's built with FastAPI and Celery for background task processing, consuming events from RabbitMQ for notification triggers.

## Directory Structure

```
services/notification-service/
├── app/
│   ├── main.py
│   ├── api/routes_notifications.py
│   ├── core/config.py
│   ├── core/security.py
│   ├── services/push_service.py
│   ├── workers/celery_app.py
│   ├── workers/tasks.py
│   ├── consumers/todo_consumer.py
│   ├── models/push_subscription.py
│   ├── schemas/notification.py
│   └── db/session.py
├── migrations/ (Alembic)
├── tests/
│   ├── unit/
│   └── integration/
└── Dockerfile
```

## Core Models

### PushSubscription
- **Fields:** Id, UserId, Endpoint, P256dh, Auth, CreatedAt
- **Constraints:** User must be unique (one subscription per user)
- **Purpose:** Store WebPush subscription details for push notifications

### NotificationLog
- **Fields:** Id, UserId, Type, Title, Body, SentAt, Status (PENDING, SENT, FAILED)
- **Constraints:** None
- **Purpose:** Track notification delivery attempts and results

## Key Endpoints

| Method | Endpoint | Description | Access | Request Body | Response Body |
|--------|----------|-------------|---------|--------------|---------------|
| POST | /api/v1/notifications/subscribe | Register push subscription | USER | PushSubscription | SubscriptionResponse |
| POST | /api/v1/notifications/unsubscribe | Remove push subscription | USER | None | SuccessResponse |
| GET | /api/v1/notifications/history | Notification log | USER | Query params | List[NotificationLog] |
| POST | /api/v1/notifications/test | Send test notification | USER | TestNotification | NotificationResponse |

### Request/Response Schemas

**PushSubscription**
```json
{
  "endpoint": "string",
  "p256dh": "string",
  "auth": "string"
}
```

**TestNotification**
```json
{
  "title": "string",
  "body": "string"
}
```

**NotificationLog**
```json
{
  "id": "uuid",
  "type": "string",
  "title": "string",
  "body": "string",
  "sentAt": "ISO-8601",
  "status": "PENDING|SENT|FAILED"
}
```

## Services

### PushService
- **Responsibilities:**
  - Send push notifications via WebPush API
  - Handle VAPID authentication
  - Manage notification failures and retries
- **Key Methods:**
  - `send(subscription, message, vapid_private_key, vapid_claims)`: bool
  - `validate_subscription(subscription)`: bool

### TodoConsumer
- **Responsibilities:**
  - Consume todo events from RabbitMQ
  - Trigger notifications based on event type
- **Key Events Handled:**
  - `todo.created`: Send notification for new todos with reminders
  - `todo.completed`: Send completion notification
- **Key Methods:**
  - `process_todo_event(event)`: void

### NotificationHistoryService
- **Responsibilities:**
  - Track notification delivery attempts
  - Provide notification history to users
- **Key Methods:**
  - `log_notification(userId, type, title, body)`: NotificationLog
  - `get_notification_history(userId, limit)`: List[NotificationLog]

## Background Tasks (Celery)

### send_reminder_notification
- **Purpose:** Send notification for todo reminders
- **Retry Policy:** Exponential backoff, max 5 retries
- **Arguments:** user_id, todo_id, message
- **Queue:** notification-queue

### send_test_notification
- **Purpose:** Send test notification to user
- **Arguments:** user_id, title, body
- **Queue:** notification-queue

## Configuration

### Key Settings (config.py)
```python
class Settings(BaseSettings):
    # API Settings
    api_name: str = "Notification Service"
    api_version: str = "v1"
    
    # Database Settings
    database_url: str = "postgresql+psycopg://postgres:secret@localhost:5432/notification_db"
    
    # Auth Settings
    jwt_jwks_uri: str = "http://auth-service:5001/api/v1/auth/jwks"
    
    # RabbitMQ Settings
    rabbitmq_host: str = "localhost"
    rabbitmq_port: int = 5672
    rabbitmq_user: str = "guest"
    rabbitmq_password: str = "guest"
    rabbitmq_vhost: str = "/"
    
    # Redis Settings (Celery broker)
    redis_url: str = "redis://localhost:6379/0"
    
    # VAPID Settings
    vapid_private_key: str = "path/to/private.key"
    vapid_subject: str = "mailto:admin@todo-app.com"
    vapid_claims: dict = {"sub": "mailto:admin@todo-app.com"}
```

### Environment Variables
- **DATABASE_URL:** PostgreSQL connection string
- **JWT_JWKS_URI:** Auth service JWKS endpoint URL
- **RABBITMQ_*:** RabbitMQ connection parameters
- **REDIS_URL:** Redis connection URL (Celery broker)
- **VAPID_PRIVATE_KEY:** VAPID private key file path

## Security

### JWT Authentication
- **Validation:** Validate JWT tokens using Auth Service JWKS endpoint
- **Claims:** Extract user ID, roles, and approved status
- **Authorization:** All endpoints require authenticated user

### WebPush Security
- **VAPID Authentication:** Use VAPID keys for push notification authentication
- **Endpoint Validation:** Validate push subscription endpoints
- **Content Security:** Ensure notification content is safe

## Error Handling

### Standard Error Response
```json
{
  "code": "HTTP_ERROR",
  "message": "Error message",
  "path": "/api/v1/notifications"
}
```

### Custom Exceptions
- **InvalidSubscriptionException:** Invalid push subscription
- **NotificationFailedException:** Push notification delivery failed
- **RateLimitException:** Too many notification requests
- **AuthenticationException:** Invalid or expired token

### Celery Task Error Handling
- **Retry on Failure:** Automatic retry with exponential backoff
- **Dead Letter Queue:** Move failed tasks to DLQ after max retries
- **Error Logging:** Log task failures with context

## Testing

### Unit Tests
- **PushService:** Test WebPush notification sending
- **TodoConsumer:** Test event processing
- **Celery Tasks:** Test task execution and retry logic

### Integration Tests
- **Endpoint Testing:** Test all API endpoints
- **RabbitMQ Testing:** Test event consumption
- **Redis Testing:** Test Celery broker connectivity

## Deployment

### Container Configuration
- **Base Image:** python:3.12-slim
- **Port:** 8002
- **Dependencies:** FastAPI, Celery, WebPush, SQLAlchemy

### Docker Compose Configuration
```yaml
services:
  notification-service:
    build: ./services/notification-service
    ports:
      - "8002:8002"
    environment:
      - DATABASE_URL=postgresql+psycopg://postgres:secret@postgres-notification:5432/notification_db
      - JWT_JWKS_URI=http://auth-service:5001/api/v1/auth/jwks
      - RABBITMQ_HOST=rabbitmq
      - REDIS_URL=redis://redis:6379/0
    depends_on:
      - postgres-notification
      - redis
      - rabbitmq
```

### Kubernetes Considerations
- **Celery Worker:** Separate deployment from API server
- **Auto-scaling:** Scale workers based on queue length
- **Secrets:** VAPID keys and database credentials

## Monitoring

### Celery Monitoring
- **Task Queue Length:** Monitor pending tasks
- **Task Success Rate:** Track successful vs failed tasks
- **Worker Status:** Monitor active/idle workers

### Notification Metrics
- `notifications_sent_total`: Total notifications sent
- `notifications_failed_total`: Failed notifications
- `notification_delivery_duration_ms`: Delivery time
- `push_subscription_count`: Active subscriptions

### RabbitMQ Monitoring
- **Queue Depth:** Monitor todo.event queue
- **Message Rate:** Track event consumption rate

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-02 | Architecture Team | Initial LLD for Notification Service |
