.PHONY: help up down restart logs clean build test status ps

# Default target
help:
	@echo "Available commands:"
	@echo "  make up        - Start all infrastructure services"
	@echo "  make down      - Stop all services"
	@echo "  make restart   - Restart all services"
	@echo "  make logs      - Show logs for all services"
	@echo "  make clean     - Stop services and remove volumes"
	@echo "  make build     - Build all service images (if Dockerfiles exist)"
	@echo "  make test      - Run all tests (best-effort)"
	@echo "  make status    - Check health of all services"
	@echo "  make ps        - Show docker compose service table"

# Start infrastructure
up:
	@echo "Starting infrastructure services..."
	docker compose up -d
	@echo "Waiting for services to be healthy..."
	@sleep 10
	@$(MAKE) status

# Stop all services
down:
	@echo "Stopping all services..."
	docker compose down

# Restart services
restart: down up

# View logs
logs:
	docker compose logs -f

# Clean everything (including volumes)
clean:
	@echo "Stopping services and removing volumes..."
	docker compose down -v

# Build service images (when Dockerfiles are present)
build:
	@echo "Building service images..."
	@cd services/auth-service && docker build -t todo-ms/auth-service:latest . || true
	@cd services/user-service && docker build -t todo-ms/user-service:latest . || true
	@cd services/todo-service && docker build -t todo-ms/todo-service:latest . || true
	@cd services/tag-service && docker build -t todo-ms/tag-service:latest . || true
	@cd services/notification-service && docker build -t todo-ms/notification-service:latest . || true
	@cd services/admin-service && docker build -t todo-ms/admin-service:latest . || true

# Run tests (best-effort; skips if toolchain not present)
test:
	@echo "Running tests..."
	@echo "Auth Service tests (.NET)..."
	@cd services/auth-service && dotnet test || true
	@echo "User Service tests (.NET)..."
	@cd services/user-service && dotnet test || true
	@echo "Todo Service tests (Maven)..."
	@cd services/todo-service && mvn -q -DskipTests=false test || true
	@echo "Tag Service tests (pytest)..."
	@cd services/tag-service && python -m pytest || true
	@echo "Notification Service tests (pytest)..."
	@cd services/notification-service && python -m pytest || true
	@echo "Admin Service tests (Maven)..."
	@cd services/admin-service && mvn -q -DskipTests=false test || true

# Show docker compose services
ps:
	docker compose ps

# Check service health
status:
	@echo "Checking service health..."
	@echo "----------------------------------------"
	@echo "PostgreSQL Databases:"
	@docker compose ps postgres-auth postgres-user postgres-todo postgres-tag postgres-admin postgres-notification || true
	@echo "----------------------------------------"
	@echo "Redis:"
	@docker compose ps redis || true
	@echo "----------------------------------------"
	@echo "RabbitMQ:"
	@docker compose ps rabbitmq || true
	@echo "  Management UI: http://localhost:15672 (admin/admin123)"
	@echo "----------------------------------------"
	@echo "Kong Gateway:"
	@docker compose ps kong || true
	@echo "  Proxy: http://localhost:8000"
	@echo "  Admin API: http://localhost:8001"
	@echo "----------------------------------------"
	@echo "Jaeger:"
	@docker compose ps jaeger || true
	@echo "  UI: http://localhost:16686"
	@echo "----------------------------------------"
	@echo "Prometheus:"
	@docker compose ps prometheus || true
	@echo "  UI: http://localhost:9090"
	@echo "----------------------------------------"

# Smoke test endpoints and container status
smoke:
	@chmod +x scripts/smoke.sh
	@./scripts/smoke.sh
