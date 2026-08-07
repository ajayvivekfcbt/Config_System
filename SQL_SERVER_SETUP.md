# Azure App Service + SQL Database Setup - Summary

**Your Config System is now configured for production with managed SQL Server**

## What's Been Updated

✅ **Code Updated to Use SQL Server**
- Reverted from SQLite to `EntityFrameworkCore.SqlServer`
- Updated `Program.cs` to use `UseSqlServer()` 
- Uses `Migrate()` instead of `EnsureCreated()`

✅ **Configuration Updated**
- `appsettings.Production.json` points to Azure SQL Database
- Connection strings use Azure SQL Server format
- Environment variables for secure credential management

✅ **New Deployment Guide**
- [APPSERVICE_WITH_SQL_DEPLOYMENT.md](APPSERVICE_WITH_SQL_DEPLOYMENT.md) - Complete 8-step guide

✅ **Updated Deployment Script**
- [deploy-appservice-sql.bat](deploy-appservice-sql.bat) - One-click deployment with SQL Database creation

---

## Architecture

```
Azure App Service (.NET 10)
        ↓
Azure SQL Database (Managed)
  - ConfigSystem (production)
  - ConfigSystem_Dev (dev)
  - ConfigSystem_Fcb (fcb)
  - Automatic backups
  - Encryption at rest/transit
```

---

## Quick Start

### 1. Update Environment Variables
```powershell
# Edit .env with your SQL Server credentials
SQL_SERVER_NAME=configsystem-sql-1234
SQL_USER=sqladmin
SQL_PASSWORD=YourSecurePassword123!
```

### 2. Run One-Click Deployment
```powershell
# This creates:
# - Resource group
# - Azure SQL Server + 3 databases
# - App Service Plan
# - Backend & Frontend App Services
# - Deploys all code

.\deploy-appservice-sql.bat
```

### 3. Access Your Application
- **Frontend**: https://configsystem-web-prod.azurewebsites.net
- **API**: https://configsystem-api-prod.azurewebsites.net
- **Docs**: https://configsystem-api-prod.azurewebsites.net/swagger
- **SQL Server**: configsystem-sql-XXXX.database.windows.net

---

## Cost Breakdown

| Component | Cost |
|-----------|------|
| App Service Plan (S1) | $50/month |
| Azure SQL Database (Basic) | $15/month |
| Storage & Backup | $5/month |
| **Total** | **~$70/month** |

**Free tier available for testing:**
- App Service F1: Free (limited)
- SQL Database: Free tier ~40 hours/month
- Total: ~$15/month

---

## Database Setup

The databases are created automatically with this structure:

```sql
-- Production database
CREATE DATABASE ConfigSystem

-- Development database  
CREATE DATABASE ConfigSystem_Dev

-- FCB staging database
CREATE DATABASE ConfigSystem_Fcb
```

Each database has the same schema:
- Configuration tables (from UTCFG* CSV files)
- Entity Framework migration history
- GoAnywhere sync tables (optional)

---

## Connection Strings

The app connects to SQL Server using:

```
Server=tcp:{SQL_SERVER_NAME}.database.windows.net,1433;
Initial Catalog=ConfigSystem|Dev|Fcb;
Persist Security Info=False;
User ID={SQL_USER};
Password={SQL_PASSWORD};
MultipleActiveResultSets=False;
Encrypt=True;
TrustServerCertificate=False;
Connection Timeout=30;
```

**Important**: 
- The `X-Config-Source` header determines which database to use (Dev, Fcb, or Default)
- Credentials are passed via environment variables (not hardcoded)
- Connections use TLS encryption

---

## Data Seeding

On first startup, the application:

1. **Checks if databases exist** - Creates schema if needed
2. **Runs Entity Framework migrations** - Applies any pending schema changes
3. **Imports CSV data** - Loads UTCFG* files from `backend/ConfigSystem.Api/Data/`
4. **Seeds default values** - Creates fallback data if imports fail
5. **Initializes FCB scope** - Sets up FCB 400 staging (if needed)

Check logs to verify:
```powershell
az webapp log tail --resource-group config-system-rg --name configsystem-api-prod
# Look for: "[STARTUP] ✓ Source ConfigSystem initialized successfully"
```

---

## Security Features

✅ **Encryption**
- TLS 1.2 for connections to SQL Server
- Encryption at rest in SQL Database
- Passwords stored in environment variables only

✅ **Access Control**
- SQL Server firewall allows only Azure services
- Your IP can be whitelisted for admin access
- App Service uses managed identity (optional)

✅ **Backup & Recovery**
- Automatic daily backups (35-day retention)
- Geo-redundant storage
- Point-in-time restore available

---

## Monitoring

### Application Insights (Optional)
```powershell
# Create Application Insights
az monitor app-insights component create \
  --resource-group config-system-rg \
  --app config-system-insights

# Connect in Azure Portal: App Service → Monitoring → Application Insights
```

### Logs
```powershell
# Stream live logs
az webapp log tail --resource-group config-system-rg --name configsystem-api-prod

# View SQL Server logs
# Azure Portal → SQL Database → Query Editor → sys.dm_exec_requests
```

---

## Next Steps

1. ✅ Run the deployment script: `.\deploy-appservice-sql.bat`
2. ✅ Wait for deployment (10-15 minutes)
3. ✅ Access your app at the URLs above
4. ✅ Upload CSV data or use API to seed configurations
5. ✅ Configure custom domain (optional)
6. ✅ Set up continuous deployment (optional)

---

## Files Changed

| File | Change |
|------|--------|
| `ConfigSystem.Api.csproj` | Updated to use `EntityFrameworkCore.SqlServer` |
| `Program.cs` | Changed from `UseSqlite()` to `UseSqlServer()` |
| `appsettings.Production.json` | Updated connection strings for Azure SQL |
| `.env.example` | Updated with SQL Server variables |

---

## Files Created

| File | Purpose |
|------|---------|
| `APPSERVICE_WITH_SQL_DEPLOYMENT.md` | Complete 8-step deployment guide |
| `APPSERVICE_QUICKSTART.md` | Quick reference guide |
| `deploy-appservice-sql.bat` | One-click deployment script |
| `deploy-appservice.sh` | One-click deployment for Linux/Mac |

---

## Troubleshooting

### "Connection timeout"
```powershell
# Check SQL Server firewall
az sql server firewall-rule list --resource-group config-system-rg --server {SERVER_NAME}

# Verify app settings
az webapp config appsettings list --resource-group config-system-rg --name configsystem-api-prod
```

### "Database not found"
```powershell
# Verify databases were created
az sql db list --resource-group config-system-rg --server {SERVER_NAME}

# Check logs for migration errors
az webapp log tail --resource-group config-system-rg --name configsystem-api-prod
```

### "Login failed"
```powershell
# Verify credentials
echo "User: {SQL_USER}"
echo "Server: {SQL_SERVER_NAME}.database.windows.net"

# Test connection with SQL Server Management Studio
# Server: {SERVER_NAME}.database.windows.net
# Auth: SQL authentication
# User: {SQL_USER}
# Password: {SQL_PASSWORD}
```

---

## Useful Azure CLI Commands

```powershell
# List all SQL databases
az sql db list --resource-group config-system-rg --server {SERVER_NAME}

# Delete a database
az sql db delete --resource-group config-system-rg --server {SERVER_NAME} --name ConfigSystem

# Check SQL Server details
az sql server show --name {SERVER_NAME} --resource-group config-system-rg

# Restart App Service
az webapp restart --resource-group config-system-rg --name configsystem-api-prod

# Stream logs
az webapp log tail --resource-group config-system-rg --name configsystem-api-prod

# Delete entire resource group (all resources)
az group delete --resource-group config-system-rg
```

---

## Support & Documentation

- **Azure App Service**: https://learn.microsoft.com/en-us/azure/app-service/
- **Azure SQL Database**: https://learn.microsoft.com/en-us/azure/azure-sql/database/
- **Entity Framework Core**: https://learn.microsoft.com/en-us/ef/core/
- **Azure CLI**: https://learn.microsoft.com/en-us/cli/azure/
- **Pricing**: https://azure.microsoft.com/en-us/pricing/

---

**Your Config System is ready for production deployment with Azure! 🎉**

See [APPSERVICE_WITH_SQL_DEPLOYMENT.md](APPSERVICE_WITH_SQL_DEPLOYMENT.md) for detailed instructions.
