# Production Deployment Checklist

Use this checklist to prepare your Config System for production Docker deployment.

## Pre-Deployment Checklist

### Code & Dependencies
- [ ] All code committed to version control
- [ ] No `appsettings.Development.json` in production builds
- [ ] `appsettings.Production.json` created with environment variables
- [ ] NuGet packages updated: `dotnet nuget update`
- [ ] Frontend dependencies updated: `npm update`
- [ ] No hardcoded credentials in source code

### Configuration Files
- [ ] `.env.example` created with all required variables
- [ ] `.env` copied from `.env.example` (NOT in git)
- [ ] `.gitignore` includes `.env`, `*.db`, `/bin`, `/obj`, `node_modules`
- [ ] `appsettings.Production.json` uses `${VARIABLE}` placeholders
- [ ] Connection strings don't contain hardcoded passwords

### Docker Setup
- [ ] `backend/Dockerfile` created
- [ ] `backend/.dockerignore` created
- [ ] `frontend/Dockerfile` created
- [ ] `frontend/nginx.conf` created
- [ ] `docker-compose.yml` created with all services
- [ ] `nginx/nginx.conf` created (reverse proxy)
- [ ] `scripts/init-db.sql` created (database initialization)

### Database Migration
- [ ] Project file updated: `EntityFrameworkCore.SqlServer` package added
- [ ] Project file updated: `EntityFrameworkCore.Sqlite` package removed
- [ ] `Program.cs` updated: `UseSqlite()` → `UseSqlServer()`
- [ ] `Program.cs` updated: `EnsureCreated()` → `Migrate()`
- [ ] Entity Framework migrations created (if using Migrations folder)
- [ ] Data import/seeding scripts validated

### Frontend Configuration
- [ ] `vite.config.ts` configured for production API proxy
- [ ] Build command tested: `npm run build`
- [ ] Production environment variables documented
- [ ] API URL configured correctly in `.env` / environment

### Security Review
- [ ] SQL Server SA password is strong (8+ chars, mixed case, numbers, symbols)
- [ ] `.env` file permissions restricted (not world-readable)
- [ ] No secrets committed to git (add to `.gitignore`)
- [ ] CORS policy reviewed and restricted if possible
- [ ] Authentication service configured (`As400AuthService`)
- [ ] SSL/TLS certificates ready (or generated)

### Network & DNS
- [ ] Domain name configured (if using HTTPS)
- [ ] DNS records point to correct IP/server
- [ ] Firewall rules allow ports 80, 443 (or custom ports)
- [ ] Database port 1433 is NOT exposed to internet (only internal to Docker)
- [ ] API port 5000 is NOT directly exposed (only via Nginx proxy)

### Testing
- [ ] `docker-compose build` completes successfully
- [ ] `docker-compose up -d` starts all services
- [ ] SQL Server is accessible and healthy
- [ ] API startup completes without errors (`docker-compose logs api`)
- [ ] API health endpoint responds: `curl http://localhost:5000/health`
- [ ] Frontend loads and displays correctly
- [ ] Frontend can call API endpoints successfully
- [ ] Database seeding/migrations complete successfully
- [ ] Authentication workflow tested (login/logout)
- [ ] Configuration data retrieves correctly from SQL Server

## Deployment Steps

### 1. System Preparation
```bash
# Install Docker Desktop
# https://www.docker.com/products/docker-desktop

# Verify Docker/Compose
docker --version
docker-compose --version
```

### 2. Environment Setup
```bash
# Clone/pull latest code
git clone <repo> /opt/config-system
cd /opt/config-system

# Create .env file
cp .env.example .env

# Edit .env with production values
nano .env  # or: code .env, vim .env, etc.
```

### 3. Build & Deploy
```bash
# Option A: Use quick start script
./start-docker.bat   # Windows
bash start-docker.sh # Linux/Mac

# Option B: Manual
docker-compose build
docker-compose up -d
docker-compose logs -f
```

### 4. Health Checks
```bash
# Verify services are running
docker-compose ps

# API health check
curl -f http://localhost:5000/health
# Expected: 200 OK

# Frontend access
curl -I http://localhost:3000
# Expected: 200 OK

# SQL Server connectivity
docker exec config-system-db sqlcmd -S localhost -U sa -P <SA_PASSWORD> -Q "SELECT 1"
# Expected: (1 row affected)
```

### 5. Data Verification
```bash
# Check if databases were created
docker exec config-system-db sqlcmd -S localhost -U sa -P <SA_PASSWORD> -Q "SELECT name FROM sys.databases WHERE name LIKE 'ConfigSystem%'"

# Check if tables were seeded
docker exec config-system-db sqlcmd -S localhost -U sa -P <SA_PASSWORD> -Q "USE ConfigSystem; SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES"

# View API logs for seeding results
docker-compose logs api | grep -i "seed\|import\|startup"
```

## Post-Deployment Checklist

- [ ] All services running and healthy (`docker-compose ps`)
- [ ] API responding to requests
- [ ] Frontend loading and functional
- [ ] Database populated with expected data
- [ ] SSL/TLS configured (if HTTPS is required)
- [ ] Backups scheduled
- [ ] Monitoring/alerting configured
- [ ] Documentation updated with production URLs
- [ ] Team trained on deployment/maintenance procedures

## Maintenance Tasks

### Daily
- [ ] Monitor error logs: `docker-compose logs api`
- [ ] Check disk space (especially SQL Server data volumes)
- [ ] Verify all containers are running: `docker-compose ps`

### Weekly
- [ ] Backup database: `docker exec config-system-db sqlcmd ... BACKUP DATABASE`
- [ ] Review application logs for errors/warnings
- [ ] Test failover/recovery procedures

### Monthly
- [ ] Security updates: `docker-compose down && docker-compose pull && docker-compose build --no-cache`
- [ ] Performance analysis: database indexes, slow queries
- [ ] Update documentation as needed

### Quarterly
- [ ] Security audit: review RBAC, firewall rules, credentials rotation
- [ ] Capacity planning: assess disk/memory/CPU needs
- [ ] Disaster recovery drill: test backup restoration

## Troubleshooting Reference

| Issue | Command to Debug |
|-------|------------------|
| API won't start | `docker-compose logs api` |
| SQL Server unreachable | `docker exec config-system-api curl http://sqlserver:80` |
| Database not created | `docker exec config-system-db sqlcmd -S localhost -U sa -Q "SELECT name FROM sys.databases"` |
| Frontend gets 404 | `docker-compose logs frontend` |
| CORS errors | Check `appsettings.Production.json`, review browser console |
| High CPU/Memory | `docker stats` |
| Container crashes | `docker-compose logs <service-name> --tail 100` |

## Rollback Procedure

If deployment fails or issues arise:

```bash
# 1. Stop all services
docker-compose down

# 2. Revert to previous working version
git checkout <previous-tag>
cd backend && git checkout <previous-tag>

# 3. Rebuild and restart
docker-compose build --no-cache
docker-compose up -d

# 4. Verify
docker-compose ps
curl http://localhost:5000/health
```

## Support & Resources

- Docker Documentation: https://docs.docker.com/
- Entity Framework Core: https://docs.microsoft.com/en-us/ef/core/
- SQL Server on Linux: https://learn.microsoft.com/en-us/sql/linux/
- Nginx Docs: https://nginx.org/en/docs/
- .NET Docs: https://learn.microsoft.com/en-us/dotnet/

---

**Last Updated**: [Date]
**Deployed By**: [Name]
**Production Environment**: [Server/Cloud Provider]
