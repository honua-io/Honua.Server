# Quick Start Guide - Process Framework Testing Stack

## Start the Stack (One Command)

```bash
cd /home/mike/projects/HonuaIO/docker/process-testing
./scripts/start-testing-stack.sh
```

## Access Services

| Service | URL | Credentials |
|---------|-----|-------------|
| Grafana Dashboard | http://localhost:3000/d/honua-process-framework | admin / admin |
| Prometheus | http://localhost:9090 | N/A |
| Grafana Home | http://localhost:3000 | admin / admin |

## Run Honua.Cli.AI with Testing Configuration

```bash
cd /home/mike/projects/HonuaIO
export ASPNETCORE_ENVIRONMENT=Testing
dotnet run --project src/Honua.Cli.AI
```

## Verify Everything Works

```bash
cd /home/mike/projects/HonuaIO/docker/process-testing
./scripts/verify-health.sh
```

## Stop the Stack

```bash
./scripts/stop-testing-stack.sh
```

## Troubleshooting

If services don't start:
1. Check Docker is running: `docker info`
2. Check port conflicts: `lsof -i :6379 -i :9090 -i :3000`
3. View logs: `docker-compose logs -f`
4. Run health check: `./scripts/verify-health.sh`

For detailed documentation, see README.md
