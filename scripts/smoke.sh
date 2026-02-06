#!/usr/bin/env bash
set -euo pipefail

echo "Infra smoke check - $(date -Iseconds)"

declare -a URLS=(
  "Kong Proxy /httpbin/status/200|http://localhost:8000/httpbin/status/200"
  "HTTPBin direct|http://localhost:18080/status/200"
  "Kong Admin /status|http://localhost:8001/status"
  "Kong Admin /metrics|http://localhost:8001/metrics"
  "Prometheus /-/ready|http://localhost:9090/-/ready"
  "Grafana /api/health|http://localhost:3001/api/health"
  "RabbitMQ UI /|http://localhost:15672/"
  "Jaeger UI /|http://localhost:16686/"
)

pad() { printf "%-24s" "$1"; }

for entry in "${URLS[@]}"; do
  name="${entry%%|*}"
  url="${entry#*|}"
  code=$(curl -s -o /dev/null -w "%{http_code}" "$url" || echo "000")
  printf "%s %s -> %s\n" "$(pad "$name")" "$url" "$code"
done

echo
echo "docker compose ps:"
docker compose ps
