# Sequence Diagrams - Todo Microservices Solution

Version: 1.0
Date: February 5, 2026
Status: Approved

This document provides end-to-end sequence diagrams covering core user and system flows across the polyglot microservices architecture routed via the API Gateway (Kong). These flows align with ADR-001, HLD, and LLD.

Contents
- User Registration and Admin Approval
- User Login and JWT Validation via JWKS
- Create Todo with Tags and Event Publication
- Reminder Notification Scheduling and Delivery
- Admin Approval Event Publication and Impact

---

## 1) User Registration and Admin Approval

```mermaid
sequenceDiagram
    autonumber
    participant U as User
    participant FE as Frontend (React)
    participant GW as API Gateway (Kong)
    participant AUTH as Auth Service (.NET)
    participant USER as User Service (.NET)
    participant ADM as Admin Service (Spring)
    participant DBU as user_db (Postgres)
    participant DBA as admin_db (Postgres)

    rect rgba(200, 255, 200, 0.2)
    Note over U: Registration
    U->>FE: Fill Registration Form
    FE->>GW: POST /api/v1/auth/register {username,email,password}
    GW->>AUTH: Route request
    AUTH-->>FE: 200 { userId, status: "PENDING" }
    end

    rect rgba(200, 200, 255, 0.2)
    Note over U,ADM: Admin Approval
    U->>FE: Login as Admin
    FE->>GW: POST /api/v1/auth/login {admin creds}
    GW->>AUTH: Route
    AUTH-->>FE: 200 { accessToken, refreshToken }

    FE->>GW: GET /api/v1/admin/users/pending (Bearer)
    GW->>ADM: Route
    ADM->>DBU: Query PENDING users
    ADM-->>FE: 200 [ {userId, username, ...} ]

    FE->>GW: POST /api/v1/admin/users/{id}/approve (Bearer)
    GW->>ADM: Route
    ADM->>DBU: Update status -> ACTIVE
    ADM->>DBA: Insert audit action
    ADM-->>FE: 204 No Content
    end
```

Notes:
- Registration handled by Auth Service; profile details exist in User Service (created on first login or via background sync, depending on implementation strategy).
- Admin approval flips status to ACTIVE. Pending users cannot log in.

---

## 2) User Login and JWT Validation via JWKS

```mermaid
sequenceDiagram
    autonumber
    participant U as User
    participant FE as Frontend (React)
    participant GW as API Gateway (Kong)
    participant AUTH as Auth Service (.NET)
    participant TODO as Todo Service (Spring)
    participant JWKS as AUTH /auth/jwks

    rect rgba(200, 255, 200, 0.2)
    Note over U: Login
    U->>FE: Enter credentials
    FE->>GW: POST /api/v1/auth/login {username,password}
    GW->>AUTH: Route
    AUTH-->>FE: 200 { accessToken (RS256), refreshToken, exp }
    end

    rect rgba(255, 240, 200, 0.2)
    Note over FE,TODO: Authorized Request
    FE->>GW: GET /api/v1/todos (Authorization: Bearer <JWT>)
    GW->>TODO: Route request with JWT header

    par First time JWKS fetch (cached)
      TODO->>JWKS: GET /.well-known JWKS (auth/jwks)
      JWKS-->>TODO: 200 JWKS (public keys)
    and JWT validation
      TODO->>TODO: Validate signature (kid), iss, aud, exp
      TODO->>TODO: Extract claims: sub, roles, approved
    end
    TODO-->>GW: 200 [todos...]
    GW-->>FE: 200 [todos...]
    end
```

Notes:
- Services cache JWKS and refresh periodically or on cache miss.
- If approved != true in token claims, requests are rejected with 403.

---

## 3) Create Todo with Tags and Event Publication

```mermaid
sequenceDiagram
    autonumber
    participant U as User
    participant FE as Frontend (React)
    participant GW as API Gateway (Kong)
    participant TODO as Todo Service (Spring)
    participant TAG as Tag Service (FastAPI)
    participant RAB as RabbitMQ
    participant DBT as todo_db (Postgres)
    participant DBTG as tag_db (Postgres)

    rect rgba(200, 255, 200, 0.2)
    Note over U: Create Todo
    U->>FE: Submit {title, description, tags[], reminderDate?}
    FE->>GW: POST /api/v1/todos (Bearer)
    GW->>TODO: Route

    TODO->>DBT: INSERT todo (ownerId=sub)
    alt reminderDate provided
      TODO->>RAB: Publish event "todo.created" {todoId, userId, reminderDate}
    end
    TODO-->>FE: 201 {todo}
    end

    rect rgba(200, 200, 255, 0.2)
    Note over FE,TAG: Associate Tags (optional)
    loop for each tag in tags[]
      FE->>GW: POST /api/v1/tags (create if needed)
      GW->>TAG: Route
      TAG->>DBTG: INSERT tag (user-scoped unique)
      TAG-->>FE: 201 {tag}

      FE->>GW: POST /api/v1/tags/{tagId}/todos/{todoId}
      GW->>TAG: Route
      TAG->>DBTG: INSERT todo_tags (todoId, tagId)
      TAG-->>FE: 204
    end
    end
```

Notes:
- Tag Service does not synchronously validate todo existence; it relies on trusted IDs and eventual consistency policies.
- Event “todo.created” is consumed by Notification Service to schedule reminders.

---

## 4) Reminder Notification Scheduling and Delivery

```mermaid
sequenceDiagram
    autonumber
    participant RAB as RabbitMQ
    participant NOTIF as Notification Service (FastAPI)
    participant CEL as Celery Worker
    participant RED as Redis (Broker/Backend)
    participant DBN as notification_db (Postgres)
    participant BROW as Browser (Service Worker)

    rect rgba(220, 240, 255, 0.3)
    Note over RAB,NOTIF: Event Consumption
    RAB-->>NOTIF: todo.created { userId, todoId, reminderDate }
    NOTIF->>CEL: Enqueue schedule task { runAt=reminderDate }
    CEL->>RED: Persist scheduled job
    end

    rect rgba(220, 255, 220, 0.3)
    Note over CEL,BROW: Delivery
    CEL->>CEL: On reminderDate trigger task
    CEL->>DBN: Fetch user subscription (endpoint,p256dh,auth)
    CEL->>BROW: WebPush send (VAPID)
    CEL->>DBN: INSERT notification_log { status=DELIVERED/FAILED }
    end
```

Notes:
- VAPID keys are configured in Notification Service; failures logged and retried with exponential backoff.
- If subscription is invalid/expired, it’s pruned.

---

## 5) Admin Approval Event Publication and Impact

```mermaid
sequenceDiagram
    autonumber
    participant ADM as Admin Service (Spring)
    participant RAB as RabbitMQ
    participant NOTIF as Notification Service (FastAPI)
    participant DBN as notification_db (Postgres)
    participant FE as Frontend (React)

    rect rgba(255, 245, 220, 0.3)
    Note over ADM: Approval
    ADM->>RAB: Publish user.approved {userId, approvedBy, approvedAt}
    end

    rect rgba(240, 255, 240, 0.3)
    Note over NOTIF: Consume Event
    RAB-->>NOTIF: user.approved { ... }
    NOTIF->>DBN: Fetch subscription (if any)
    alt has subscription
      NOTIF->>FE: Send WebPush "Welcome! Your account is approved."
      NOTIF->>DBN: INSERT notification_log(status=DELIVERED)
    else no subscription
      NOTIF->>DBN: INSERT notification_log(status=SKIPPED)
    end
    end
```

Notes:
- This event-driven approach decouples admin actions from notification side effects.
- Future consumers (e.g., analytics) can subscribe without changing the Admin Service.

---

## Error and Security Considerations

- All calls carry Authorization: Bearer <JWT>; services validate via JWKS and enforce approved==true.
- Gateway provides CORS, rate limiting, and request logging; services must handle 429 responses gracefully.
- Idempotency-Key supported on create endpoints (e.g., POST /todos).
- Errors follow the standard envelope with traceId for correlation across services.

---

Document History
- 1.0 (2026-02-05): Initial set of cross-service sequence diagrams
