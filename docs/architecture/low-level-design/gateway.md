# Low-Level Design (LLD) - API Gateway

**Version:** 1.0  
**Date:** February 5, 2026  
**Status:** Draft

## Overview

The API Gateway serves as the single entry point for all client requests to the Todo Microservices Solution. It's built using Kong and handles routing, authentication, rate limiting, and cross-cutting concerns before requests reach individual services.

## Architecture Overview

```
Client → Kong (Ingress) → Microservices
        ↑
      Kong Admin API
```

## Configuration

### Kong Gateway Setup
- **Version:** 3.6+
- **Persistence:** Database mode (PostgreSQL)
- **SSL:** TLS termination at gateway level
- **Health Checks:** Built-in health endpoint

### Core Kong Plugins Enabled

#### 1. Authentication
- **Plugin:** JWT
- **Configuration:** Validates JWT tokens using Auth Service JWKS endpoint
- **Settings:**
  - key_claim: sub
  - claims_to_verify: [exp, approved]
  - jwt_secret_is_base64: false
  - uri: http://auth-service:5001/api/v1/auth/jwks

#### 2. Rate Limiting
- **Plugin:** Rate Limiting
- **Configuration:** Global and per-service rate limits
- **Settings:**
  - minute: 1200 (requests per minute)
  - hour: 7200 (requests per hour)

#### 3. CORS
- **Plugin:** CORS
- **Configuration:** Allow frontend origins
- **Settings:**
  - origins: ["http://localhost:3000"]
  - methods: ["GET", "POST", "PUT", "DELETE", "OPTIONS"]
  - headers: ["*"]
  - credentials: true

#### 4. Request/Response Transformation
- **Plugin:** Request Transformer
- **Configuration:** Add correlation ID to all requests
- **Settings:**
  - add.headers: ["X-Correlation-Id: $uuid"]

#### 5. Logging
- **Plugin:** Logging
- **Configuration:** Structured JSON logging
- **Settings:**
  - log_format: json
  - log_level: info

## Service Routes

### Core Services

| Service | Route | Upstream URL | Plugins |
|---------|-------|--------------|---------|
| Auth Service | /api/v1/auth/* | http://auth-service:5001 | JWT, Rate Limiting, CORS |
| User Service | /api/v1/users/* | http://user-service:8000 | JWT, Rate Limiting, CORS |
| Todo Service | /api/v1/todos/* | http://todo-service:8004 | JWT, Rate Limiting, CORS |
| Tag Service | /api/v1/tags/* | http://tag-service:8001 | JWT, Rate Limiting, CORS |
| Notification Service | /api/v1/notifications/* | http://notification-service:8002 | JWT, Rate Limiting, CORS |
| Admin Service | /api/v1/admin/* | http://admin-service:8003 | JWT, Rate Limiting, CORS, Authentication |

### Gateway-Level Endpoints

| Method | Endpoint | Description | Access | Plugins |
|--------|----------|-------------|---------|---------|
| GET | /health | Gateway health check | PUBLIC | None |
| GET | /metrics | Prometheus metrics | PUBLIC | None |
| GET | /status | Gateway status | PUBLIC | None |

## Advanced Configuration

### Service Discovery
- **Method:** Static configuration (Docker service names)
- **Health Checks:** Kong health checks upstream services
- **Downtime Handling:** Graceful degradation with 503 responses

### Dynamic Configuration
- **Admin API:** Use Kong Admin API for route/service management
- **Environment Variables:** Configuration via environment variables
- **Config Maps:** Kubernetes config maps for Kong configuration

### Security Headers
- **Plugin:** Headers
- **Configuration:** Security headers added to all responses
- **Settings:**
  - add.headers: [
    "X-Content-Type-Options: nosniff",
    "X-Frame-Options: DENY",
    "X-XSS-Protection: 1; mode=block",
    "Strict-Transport-Security: max-age=31536000; includeSubDomains"
  ]

## Monitoring & Observability

### Kong Monitoring
- **Prometheus:** Expose metrics at /metrics
- **Health Endpoint:** GET /health
- **Status Endpoint:** GET /status
- **Logging:** Structured JSON logs

### Key Metrics
- `kong_http_requests_total`: Total HTTP requests
- `kong_http_request_duration_seconds`: Request latency
- `kong_upstream_latency_seconds`: Upstream service latency
- `kong_requests_per_second`: Request rate
- `kong_connections_active`: Active connections

### Distributed Tracing
- **Plugin:** OpenTelemetry
- **Configuration:** Trace requests across services
- **Sampling:** 10% of requests in production
- **Export:** Jaeger backend

## Error Handling

### Standard Error Responses
```json
{
  "timestamp": "ISO-8601",
  "status": 401,
  "error": "Unauthorized",
  "message": "Invalid or expired token",
  "path": "/api/v1/todos",
  "traceId": "uuid"
}
```

### Kong Error Types
- **4xx Errors:** Authentication, validation, rate limiting
- **5xx Errors:** Service unavailability, gateway errors
- **Timeouts:** Request timeouts (30s default)

### Error Handling Strategies
- **Graceful Degradation:** Return 503 if upstream service unavailable
- **Circuit Breaker:** Plugin to handle service failures
- **Retries:** Limited retries for idempotent operations

## Deployment

### Container Configuration
- **Base Image:** kong/kong:3.6-alpine
- **Port:** 8000 (HTTP), 8443 (HTTPS)
- **Volume:** /usr/local/kong/: Configuration files

### Docker Compose Configuration
```yaml
services:
  kong:
    image: kong/kong:3.6-alpine
    environment:
      KONG_DATABASE: "off"
      KONG_DECLARATIVE_CONFIG: /kong/declarative/kong.yml
      KONG_PROXY_ACCESS_LOG: /dev/stdout
      KONG_ADMIN_ACCESS_LOG: /dev/stdout
      KONG_ADMIN_LISTEN: 0.0.0.0:8001
      KONG_PROXY_LISTEN: 0.0.0.0:8000
    ports:
      - "8000:8000"
      - "8443:8443"
      - "8001:8001"
    volumes:
      - ./kong/declarative:/kong/declarative
    depends_on:
      - auth-service
      - user-service
      - todo-service
      - tag-service
      - notification-service
      - admin-service
```

### Kubernetes Configuration
- **Deployment:** 2 replicas for availability
- **Service:** LoadBalancer type
- **ConfigMap:** Kong configuration
- **Secrets:** SSL certificates
- **Ingress:** Routes external traffic to Kong

## Testing

### Unit Tests
- **Configuration Validation:** Test route and service configurations
- **Plugin Configuration:** Test plugin settings
- **Error Handling:** Test error response formats

### Integration Tests
- **Route Testing:** Test all routes to upstream services
- **Authentication:** Test JWT validation
- **Rate Limiting:** Test rate limiting behavior
- **CORS:** Test CORS headers

### Load Testing
- **Tool:** k6 or JMeter
- **Scenarios:** Simulate high traffic loads
- **Metrics:** Monitor gateway performance under load

## Future Enhancements

### Authentication Enhancements
- **OIDC Integration:** Support OpenID Connect flow
- **Service-to-Service Auth:** Mutual TLS for service communication
- **API Key Auth:** Support for API key authentication

### Advanced Features
- **Request Routing:** Dynamic routing based on headers
- **Content-Based Routing:** Route based on request content
- **WebSockets:** WebSocket support for real-time features
- **GraphQL:** GraphQL endpoint support

### Performance Optimizations
- **Caching:** Implement response caching
- **Compression:** Request/response compression
- **Load Balancing:** Advanced load balancing algorithms
- **Connection Pooling:** Optimized connection management

### Observability Improvements
- **Advanced Metrics:** Business metrics collection
- **Alerting:** Configure alerts for critical issues
- **Distributed Tracing:** Enhanced tracing capabilities
- **Logging:** Centralized log aggregation

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-05 | Architecture Team | Initial LLD for API Gateway |
