# Configuration System - Production Migration Guide

This guide explains how to migrate from SQLite development environment to SQL Server production environment using Docker.

## Overview

| Component | Development | Production |
|-----------|-------------|------------|
| Database | SQLite (file-based) | SQL Server 2022 (containerized) |
| API Server | Local .NET runtime | Docker container |
| Frontend | Vite dev server | Nginx in Docker |
| Hosting | Local machine | Docker + Docker Compose |

## Key Changes Made

### 1. **Code Changes**

#### a) Updated `ConfigSystem.Api.csproj`
- **Removed**: `Microsoft.EntityFrameworkCore.Sqlite`
- **Removed**: `SQLitePCLRaw.bundle_e_sqlite3`
- **Added**: `Microsoft.EntityFrameworkCore.SqlServer`

**Reason**: SQL Server provider replaces SQLite for production use.

#### b) Updated `Program.cs`
- **Changed DbContext configuration**: `UseSqlite()` → `UseSqlServer()`
- **Changed database initialization**: `db.Database.EnsureCreated()` → `db.Database.Migrate()`

**Reason**: Enables Entity Framework migrations for versioned schema management.

#### c) Created `appsettings.Production.json`
- Connection strings reference SQL Server container host `sqlserver`
- Supports multiple databases (ConfigSystem, ConfigSystem_Dev, ConfigSystem_Fcb)
- Uses environment variables for sensitive data

**Example**:
```json
{
  "ConnectionStrings": {
    "Default": "Server=sqlserver;Database=ConfigSystem;User Id=sa;Password=${SA_PASSWORD};Encrypt=false;"
  }
}
```

### 2. **Docker Configuration Files**

#### `docker-compose.yml`
Orchestrates 5 services:

| Service | Image | Purpose |
|---------|-------|---------|
| `sqlserver` | mcr.microsoft.com/mssql/server:2022-latest | Database server |
| `api` | config-system-api:latest | .NET backend (built from local Dockerfile) |
| `frontend` | config-system-web:latest | React UI (built from local Dockerfile) |
| `nginx` | nginx:latest | Reverse proxy, load balancer |
| (optional) | - | Scale horizontally |

#### Backend `Dockerfile`
- **Build stage**: Compiles .NET application
- **Runtime stage**: Runs only the built application
- **Multi-stage build** reduces final image size

#### Frontend `Dockerfile`
- **Build stage**: `npm install` and `npm run build` (production optimized)
- **Runtime stage**: Serves via Nginx with caching headers

### 3. **Configuration & Secrets**

`.env.example` → Copy to `.env` with production values:

```
SA_PASSWORD=YourSuperSecurePassword123!      # SQL Server admin password
IBMI_SYSTEM=10.10.1.55                       # IBM i AS/400 IP
FCB400_SYSTEM=10.10.1.52                     # FCB400 system IP
FCB400_LIBRARY=UTPRODD                       # FCB400 library
FCB400_UID=username                          # FCB400 credentials
FCB400_PWD=password
VITE_API_URL=http://localhost:5000           # Frontend API endpoint
```

**Security Note**: Never commit `.env` to version control. Add to `.gitignore`.

### 4. **Database Migration Strategy**

#### Automatic (Recommended)

The `Program.cs` startup logic automatically:

1. Creates databases (if not exists) via SQL scripts
2. Runs Entity Framework migrations
3. Seeds data from CSV files or live IBM i import
4. Handles each source (Dev, Fcb) independently

No manual database setup required—it's part of container initialization.

#### Manual (If Needed)

```sql
-- Connect to SQL Server (sqlserver:1433)
-- User: sa
-- Password: (from .env SA_PASSWORD)

CREATE DATABASE ConfigSystem;
CREATE DATABASE ConfigSystem_Dev;
CREATE DATABASE ConfigSystem_Fcb;

-- Tables auto-created by Entity Framework on first API startup
```

## Deployment Steps

### Prerequisites
```powershell
# Install Docker Desktop (includes Docker Compose)
# https://www.docker.com/products/docker-desktop

# Verify installation
docker --version
docker-compose --version
```

### Step 1: Configure Environment

```powershell
# Copy template
Copy-Item .env.example .env

# Edit with production values
notepad .env
```

**Critical settings**:
- `SA_PASSWORD`: Strong password (8+ chars, mixed case, numbers, symbols)
- `IBMI_SYSTEM` / `FCB400_*`: Correct network addresses
- `VITE_API_URL`: Match your hosting domain/port

### Step 2: Build & Deploy

#### Using Quick Start Script (Recommended)

```powershell
# Windows
.\start-docker.bat

# Linux/Mac
bash start-docker.sh
```

#### Manual Commands

```powershell
# Build Docker images
docker-compose build

# Start all services
docker-compose up -d

# Monitor logs
docker-compose logs -f

# Check health
docker-compose ps
```

### Step 3: Verify

```powershell
# Check API health
curl http://localhost:5000/health

# View frontend
Start-Process http://localhost:3000

# Test database connection (from container)
docker exec -it config-system-db sqlcmd -S localhost -U sa -P $env:SA_PASSWORD -Q "SELECT @@VERSION"

# Check logs
docker-compose logs api
docker-compose logs sqlserver
docker-compose logs frontend
```

## Troubleshooting

### Issue: "Unable to connect to SQL Server"

**Symptoms**: API logs show connection timeouts

**Solution**:
```powershell
# 1. Verify SQL Server is running
docker-compose ps sqlserver

# 2. Check logs
docker-compose logs sqlserver

# 3. Test connectivity
docker exec config-system-api curl -v http://sqlserver:1433

# 4. Verify connection string in .env
docker exec config-system-api env | findstr /i connection
```

### Issue: "Database initialization failed"

**Symptoms**: API crashes on startup with migration errors

**Solution**:
```powershell
# 1. Check API logs for specific error
docker-compose logs api | tail -50

# 2. Manually create databases if schema mismatch
docker exec -it config-system-db sqlcmd -S localhost -U sa -P <password> -i scripts/init-db.sql

# 3. Restart API after database creation
docker-compose restart api
```

### Issue: "CORS errors on frontend"

**Symptoms**: Browser console shows "blocked by CORS policy"

**Solution**:
- Check `appsettings.Production.json` has `.AllowAnyOrigin()`
- Verify `VITE_API_URL` in `.env` matches actual API location
- Check Nginx reverse proxy configuration in `nginx/nginx.conf`

### Issue: "Frontend returns 404"

**Symptoms**: Frontend loads but gets 404 on routes like `/config`

**Solution**:
- Frontend `nginx.conf` already has SPA routing (`try_files $uri $uri/ /index.html`)
- Check frontend logs: `docker-compose logs frontend`
- Verify `npm run build` ran during image build

## Production Considerations

### 1. Secrets Management

**Current**: Environment variables in `.env` file

**Better** (production):
```powershell
# Use Docker Secrets (Docker Swarm)
docker secret create sa_password -
docker secret create api_key -

# Use Azure Key Vault integration
docker run --mount type=bind,src=/secrets,dst=/mnt/secrets ...

# Use HashiCorp Vault
# Use AWS Secrets Manager
```

### 2. Backup & Recovery

```powershell
# Backup SQL Server database
docker exec config-system-db /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U sa -P $SA_PASSWORD `
  -Q "BACKUP DATABASE ConfigSystem TO DISK = '/var/opt/mssql/backup/ConfigSystem.bak'"

# Backup volumes
docker run --rm -v config_system_sqlserver-data:/data `
  -v ./backups:/backup alpine tar czf /backup/sqlserver-$(date +%Y%m%d).tar.gz -C /data .

# Schedule with cron/Task Scheduler
# Example crontab: 0 2 * * * docker-compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -Q "BACKUP DATABASE ConfigSystem TO DISK = '/var/opt/mssql/backup/ConfigSystem.bak'"
```

### 3. SSL/TLS Certificate

```powershell
# Generate self-signed cert (testing)
openssl req -x509 -newkey rsa:4096 -nodes -out cert.pem -keyout key.pem -days 365

# Place in ./nginx/ssl/
mkdir nginx/ssl
mv cert.pem nginx/ssl/
mv key.pem nginx/ssl/

# Uncomment SSL sections in nginx/nginx.conf
```

### 4. Monitoring & Logging

```yaml
# Example: Add to docker-compose.yml for centralized logging
logging:
  driver: "json-file"
  options:
    max-size: "10m"
    max-file: "3"
```

Integrate with:
- **ELK Stack** (Elasticsearch, Logstash, Kibana)
- **Azure Monitor**
- **Datadog**
- **New Relic**

### 5. Scaling & Load Balancing

```powershell
# Scale API to 3 instances
docker-compose up -d --scale api=3

# Nginx automatically round-robins between instances
```

### 6. CI/CD Integration

Example GitHub Actions workflow:

```yaml
name: Deploy to Production
on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Login to Docker Registry
        uses: docker/login-action@v2
        with:
          username: ${{ secrets.DOCKER_USERNAME }}
          password: ${{ secrets.DOCKER_PASSWORD }}
      - name: Build and push
        uses: docker/build-push-action@v4
        with:
          context: ./backend
          push: true
          tags: myrepo/config-system-api:latest
      - name: Deploy
        run: |
          ssh user@prod-server 'cd /opt/config-system && docker-compose pull && docker-compose up -d'
```

## Performance Tuning

### SQL Server
```sql
-- Enable appropriate indexes for frequently queried columns
CREATE INDEX idx_config_key ON ConfigValues(ConfigKey);
CREATE INDEX idx_source_type ON Configurations(SourceType);
```

### API
```json
// appsettings.Production.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"  // Reduce noise
    }
  }
}
```

### Frontend
- Already configured with Nginx gzip compression
- Static assets cached for 1 year
- HTML uncached to support versioning

## Maintenance

### Update Application

```powershell
# Pull latest source code
git pull origin main

# Rebuild Docker images
docker-compose build --no-cache

# Restart services
docker-compose up -d

# Verify
docker-compose logs api | tail -20
```

### SQL Server Maintenance

```sql
-- Check database size
EXEC sp_spaceused;

-- Rebuild indexes
ALTER INDEX ALL ON dbo.YourTable REBUILD;

-- Update statistics
UPDATE STATISTICS dbo.YourTable;
```

## Rollback

```powershell
# Keep previous image version available
docker tag config-system-api:latest config-system-api:backup

# To rollback
docker-compose down
docker tag config-system-api:backup config-system-api:latest
docker-compose up -d
```

## Useful Links

- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [.NET in Docker](https://learn.microsoft.com/en-us/dotnet/core/docker/)
- [SQL Server in Docker](https://learn.microsoft.com/en-us/sql/linux/quickstart-install-connect-docker)
- [Entity Framework Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Nginx Reverse Proxy](https://nginx.org/en/docs/http/ngx_http_proxy_module.html)
