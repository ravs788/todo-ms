# Project Activity Log

Purpose: Goal-oriented log of key outcomes (not step-by-step activities). Each entry captures when a goal was achieved, the focus area, and the concise result.

Format:
- Time: ISO-8601 (local timezone)
- Area: infra | dev | docs | pm | ci
- Highlights: outcome-focused summary (what value was achieved)

| Time (ISO-8601) | Area | Highlights |
|---|---|---|
| 2026-02-06T11:57:40+05:30 | infra | Resolved port conflict: remapped httpbin to 18080; validated 200 direct (18080) and via Kong (/httpbin). |
| 2026-02-05T22:24:40+05:30 | infra | Gateway e2e verified via Kong with /httpbin upstream; baseline reverse proxy functioning (200). |
| 2026-02-05T22:12:56+05:30 | infra | Local infra baseline operational via Docker Compose: Postgres (per service), Redis, RabbitMQ, Kong, Prometheus, Grafana, Jaeger; health verified via smoke script; Kong metrics exposed. |
| 2026-02-05T22:01:28+05:30 | infra | Observability foundation in place: Kong Prometheus plugin enabled; Grafana provisioned with Prometheus datasource. |
| 2026-02-05T21:28:30+05:30 | infra | API Gateway configured for all service routes (auth/users/todos/tags/notifications/admin) with CORS, correlation ID, and rate limiting. |
| 2026-02-05T21:40:09+05:30 | pm | Developer experience baseline: Makefile (up/down/status/smoke), .env.example, README quickstart updated for infra usage. |
| 2026-02-05T13:20:36+05:30 | docs | Architecture docs coherent and navigable: LLDs chained under parent, service boundaries beautified, error/ratelimiting guidance clarified, dates normalized. |

Guidelines:
- Log goals/outcomes, not granular steps.
- Prefer one entry per completed goal per area.
- Keep highlights brief and value-focused (what is now possible).
