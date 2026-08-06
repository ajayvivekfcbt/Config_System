# Azure App Service + SQL Database Deployment Guide

**Production deployment for Config System with managed SQL Server**

## Architecture

```
┌──────────────────────────────────────────────────────┐
│           Azure App Service                           │
│  ┌────────────────────────────────────────────────┐  │
│  │  Config System API (.NET 10)                   │  │
│  │  + React Frontend                              │  │
│  │  + Automatic Scaling                           │  │
│  │  + Free SSL Certificate                        │  │
│  └────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────┘
                         ↓
┌──────────────────────────────────────────────────────┐
│        Azure SQL Database (Managed)                   │
│  - ConfigSystem (prod)                               │
│  - ConfigSystem_Dev (dev)                            │
│  - ConfigSystem_Fcb (fcb)                            │
│  - Automatic backups                                 │
│  - Encryption at rest/transit                        │
└──────────────────────────────────────────────────────┘
```

---

## Prerequisites

1. **Azure Account**
   - Sign up: https://azure.microsoft.com/free
   - Free trial: $200 credits

2. **Local Tools**
   ```powershell
   # Check installations
   dotnet --version        # 10.0+
   az --version            # Latest
   npm --version           # 18+
   git --version           # Latest
   ```

3. **Environment Variables**
   - Copy `.env.example` to `.env`
   - Update with your values

---

## Step 1: Create Azure Resources

### 1.1 Login to Azure
```powershell
az login
# Opens browser for authentication
```

### 1.2 Set Variables
```powershell
$resourceGroup = "config-system-rg"
$location = "eastus"
$sqlServer = "configsystem-sql-$(Get-Random -Minimum 1000 -Maximum 9999)"
$sqlUser = "sqladmin"
$sqlPassword = "YourSecurePassword123!"  # Must be 8+ chars, mixed case, numbers, symbols
$apiAppName = "configsystem-api-prod"
$webAppName = "configsystem-web-prod"
$appServicePlan = "config-system-plan"
```

### 1.3 Create Resource Group
```powershell
az group create --name $resourceGroup --location $location
```

### 1.4 Create SQL Server
```powershell
az sql server create `
  --name $sqlServer `
  --resource-group $resourceGroup `
  --location $location `
  --admin-user $sqlUser `
  --admin-password $sqlPassword
```

### 1.5 Create SQL Databases
```powershell
# Production database
az sql db create `
  --server $sqlServer `
  --resource-group $resourceGroup `
  --name ConfigSystem `
  --edition Basic `
  --compute-model Serverless `
  --auto-pause-delay 60

# Dev database
az sql db create `
  --server $sqlServer `
  --resource-group $resourceGroup `
  --name ConfigSystem_Dev `
  --edition Basic `
  --compute-model Serverless `
  --auto-pause-delay 60

# FCB database
az sql db create `
  --server $sqlServer `
  --resource-group $resourceGroup `
  --name ConfigSystem_Fcb `
  --edition Basic `
  --compute-model Serverless `
  --auto-pause-delay 60
```

### 1.6 Configure SQL Server Firewall

Allow App Service to access SQL Server:
```powershell
# Allow Azure services
az sql server firewall-rule create `
  --resource-group $resourceGroup `
  --server $sqlServer `
  --name AllowAzureServices `
  --start-ip-address 0.0.0.0 `
  --end-ip-address 0.0.0.0

# Allow your IP (for local testing)
$myIP = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing).Content
az sql server firewall-rule create `
  --resource-group $resourceGroup `
  --server $sqlServer `
  --name AllowMyIP `
  --start-ip-address $myIP `
  --end-ip-address $myIP
```

### 1.7 Create App Service Plan
```powershell
# S1 Standard plan (~$50/month)
az appservice plan create `
  --name $appServicePlan `
  --resource-group $resourceGroup `
  --sku S1 `
  --is-linux
```

### 1.8 Create App Services
```powershell
# Backend API
az webapp create `
  --resource-group $resourceGroup `
  --plan $appServicePlan `
  --name $apiAppName `
  --runtime "DOTNETCORE|10.0"

# Frontend
az webapp create `
  --resource-group $resourceGroup `
  --plan $appServicePlan `
  --name $webAppName `
  --runtime "NODE|20"
```

---

## Step 2: Prepare & Build Application

### 2.1 Update Configuration
Edit `.env`:
```
SQL_SERVER_NAME=configsystem-sql-1234
SQL_USER=sqladmin
SQL_PASSWORD=YourSecurePassword123!
IBMI_SYSTEM=10.10.1.55
FCB400_SYSTEM=10.10.1.52
FCB400_LIBRARY=UTPRODD
FCB400_UID=username
FCB400_PWD=password
VITE_API_URL=https://configsystem-api-prod.azurewebsites.net
```

### 2.2 Build Backend
```powershell
cd backend/ConfigSystem.Api
dotnet restore
dotnet build -c Release
dotnet publish -c Release -o ./publish
cd ../..
```

### 2.3 Build Frontend
```powershell
cd frontend
npm install
npm run build
cd ..
```

### 2.4 Test Locally (Optional)
```powershell
# Start backend
cd backend/ConfigSystem.Api
dotnet run --configuration Release

# In another terminal, start frontend (with proxy)
cd frontend
npm run dev
# Frontend runs on http://localhost:5173
# API calls proxy to http://localhost:5000
```

---

## Step 3: Deploy Backend

### 3.1 Package Backend
```powershell
cd backend/ConfigSystem.Api
Compress-Archive -Path ./publish/* -DestinationPath ../../app.zip -Force
cd ../..
```

### 3.2 Deploy to App Service
```powershell
az webapp deployment source config-zip `
  --resource-group $resourceGroup `
  --name $apiAppName `
  --src-path app.zip
```

### 3.3 Configure Application Settings
```powershell
# Set environment
az webapp config appsettings set `
  --resource-group $resourceGroup `
  --name $apiAppName `
  --settings ASPNETCORE_ENVIRONMENT=Production

# Set SQL Server connection info
az webapp config appsettings set `
  --resource-group $resourceGroup `
  --name $apiAppName `
  --settings `
    "SQL_SERVER_NAME=$sqlServer" `
    "SQL_USER=$sqlUser" `
    "SQL_PASSWORD=$sqlPassword"

# Set IBM i connection info
az webapp config appsettings set `
  --resource-group $resourceGroup `
  --name $apiAppName `
  --settings `
    "IBMI_SYSTEM=10.10.1.55" `
    "FCB400_SYSTEM=10.10.1.52" `
    "FCB400_LIBRARY=UTPRODD" `
    "FCB400_UID=username" `
    "FCB400_PWD=password"
```

### 3.4 Verify Backend
```powershell
# Get app URL
$apiUrl = "https://$apiAppName.azurewebsites.net"
Write-Host "API URL: $apiUrl"

# Test health endpoint
curl "$apiUrl/health"

# View logs
az webapp log tail --resource-group $resourceGroup --name $apiAppName
```

---

## Step 4: Deploy Frontend

### 4.1 Package Frontend
```powershell
cd frontend
Compress-Archive -Path ./dist/* -DestinationPath ../frontend.zip -Force
cd ..
```

### 4.2 Deploy to App Service
```powershell
az webapp deployment source config-zip `
  --resource-group $resourceGroup `
  --name $webAppName `
  --src-path frontend.zip
```

### 4.3 Configure API URL
```powershell
az webapp config appsettings set `
  --resource-group $resourceGroup `
  --name $webAppName `
  --settings "REACT_APP_API_URL=https://$apiAppName.azurewebsites.net"
```

### 4.4 Verify Frontend
```powershell
$webUrl = "https://$webAppName.azurewebsites.net"
Write-Host "Frontend URL: $webUrl"

# Open in browser
Start-Process $webUrl
```

---

## Step 5: Initialize Database Schema

### 5.1 Connect to SQL Database
```powershell
# Using Azure Data Studio or SQL Server Management Studio
# Server: configsystem-sql-XXXX.database.windows.net
# Database: ConfigSystem
# Username: sqladmin
# Password: (from step 1.2)
```

### 5.2 Create Entity Framework Migrations (If Needed)

If you haven't created migrations:
```powershell
cd backend/ConfigSystem.Api

# Create initial migration
dotnet ef migrations add InitialCreate

# Update database
dotnet ef database update --configuration Release --connection "Server=tcp:$sqlServer.database.windows.net,1433;Initial Catalog=ConfigSystem;User ID=$sqlUser;Password=$sqlPassword;Encrypt=True;"

cd ../..
```

### 5.3 Seed Data

The application automatically seeds data on startup:
- Imports CSV files from the `data/` folder
- Creates default configurations
- Handles Dev and FCB sources separately

Check logs to verify:
```powershell
az webapp log tail --resource-group $resourceGroup --name $apiAppName
# Look for: "[STARTUP] Database initialization complete"
```

---

## Step 6: Configure HTTPS & Custom Domain

### 6.1 Enable HTTPS (Automatic)
Azure provides free SSL certificates. Just access via `https://`

### 6.2 Custom Domain (Optional)
```powershell
# Add custom domain to API
az webapp config hostname add `
  --resource-group $resourceGroup `
  --webapp-name $apiAppName `
  --hostname "api.yourdomain.com"

# Add to frontend
az webapp config hostname add `
  --resource-group $resourceGroup `
  --webapp-name $webAppName `
  --hostname "configsystem.yourdomain.com"

# Update DNS at your registrar
# CNAME: api.yourdomain.com -> configsystem-api-prod.azurewebsites.net
# CNAME: configsystem.yourdomain.com -> configsystem-web-prod.azurewebsites.net
```

---

## Step 7: Monitoring & Backup

### 7.1 Enable Application Insights
```powershell
# Create Application Insights
az monitor app-insights component create `
  --resource-group $resourceGroup `
  --app config-system-insights

# Connect to App Service (via Portal)
# App Service > Monitoring > Application Insights > Create/Select
```

### 7.2 Configure SQL Database Backups
```powershell
# Azure automatically backs up to 35 days
# Geo-redundant backups available

# View backups
az sql db list-deletions `
  --resource-group $resourceGroup `
  --server $sqlServer
```

### 7.3 Set Up Alerts
```powershell
# High CPU alert
az monitor metrics alert create `
  --name "$apiAppName-high-cpu" `
  --resource-group $resourceGroup `
  --scopes "/subscriptions/{subscription-id}/resourceGroups/$resourceGroup/providers/Microsoft.Web/sites/$apiAppName" `
  --condition "avg Percentage CPU > 80" `
  --window-size 5m `
  --evaluation-frequency 1m `
  --action email-action
```

---

## Step 8: Troubleshooting

### Issue: API won't connect to SQL
```powershell
# Check connection settings
az webapp config appsettings list `
  --resource-group $resourceGroup `
  --name $apiAppName `
  | findstr "SQL_"

# Check firewall rules
az sql server firewall-rule list `
  --resource-group $resourceGroup `
  --server $sqlServer

# View logs
az webapp log tail --resource-group $resourceGroup --name $apiAppName
```

### Issue: Database migration failed
```powershell
# Check if database exists
az sql db list `
  --resource-group $resourceGroup `
  --server $sqlServer

# Recreate if needed
az sql db delete `
  --resource-group $resourceGroup `
  --server $sqlServer `
  --name ConfigSystem

az sql db create `
  --server $sqlServer `
  --resource-group $resourceGroup `
  --name ConfigSystem `
  --edition Basic
```

### Issue: Frontend can't reach API
```powershell
# Verify API URL in frontend settings
az webapp config appsettings list `
  --resource-group $resourceGroup `
  --name $webAppName `
  | findstr "REACT_APP"

# Check frontend logs
az webapp log tail --resource-group $resourceGroup --name $webAppName

# Verify API is running
curl https://$apiAppName.azurewebsites.net/health
```

---

## Cost Estimation

| Service | Tier | Cost/Month |
|---------|------|-----------|
| App Service Plan | S1 | $50 |
| App Service (API + Web) | S1 | Included |
| SQL Database | Basic | $15 |
| Storage Redundancy | GRS | ~$5 |
| **Total** | | **~$70/month** |

**Free tier option:**
- App Service: F1 (free, limited)
- SQL Database: Free tier (40 hours/month)
- Total: ~$15/month

---

## Maintenance

### Daily
- Monitor error logs: `az webapp log tail --resource-group $resourceGroup --name $apiAppName`
- Check SQL Database CPU and storage

### Weekly
- Review Application Insights metrics
- Check backup status

### Monthly
- Update dependencies: `dotnet outdated`, `npm update`
- Review security alerts
- Analyze costs

### Quarterly
- Scale up if needed
- Plan for growth
- Update documentation

---

## Scaling

### Vertical Scaling (Larger Machine)
```powershell
# Upgrade app service plan
az appservice plan update `
  --name $appServicePlan `
  --resource-group $resourceGroup `
  --sku P1V2
```

### Horizontal Scaling (Multiple Instances)
```powershell
# Scale out to 3 instances
az appservice plan update `
  --name $appServicePlan `
  --resource-group $resourceGroup `
  --number-of-workers 3
```

### SQL Database Scaling
```powershell
# Upgrade to Standard tier
az sql db update `
  --server $sqlServer `
  --resource-group $resourceGroup `
  --name ConfigSystem `
  --edition Standard `
  --compute-model Provisioned `
  --capacity 20
```

---

## Disaster Recovery

### Backup Strategy
```powershell
# SQL Database: 35-day retention (automatic)
# App Service: Deploy slots for zero-downtime updates

# Create staging slot
az webapp deployment slot create `
  --resource-group $resourceGroup `
  --name $apiAppName `
  --slot staging

# Swap to production
az webapp deployment slot swap `
  --resource-group $resourceGroup `
  --name $apiAppName `
  --slot staging
```

### Restore from Backup
```powershell
# List available backups
az sql db list-backups `
  --resource-group $resourceGroup `
  --server $sqlServer `
  --name ConfigSystem

# Restore to new database
az sql db restore `
  --resource-group $resourceGroup `
  --server $sqlServer `
  --name ConfigSystem_Restored `
  --backup-resource-id "/subscriptions/{subId}/resourceGroups/$resourceGroup/providers/Microsoft.Sql/servers/$sqlServer/databases/ConfigSystem"
```

---

## Cleanup

Delete all resources when done:
```powershell
az group delete --resource-group $resourceGroup
```

---

## Useful Commands

```powershell
# List all resources
az resource list --resource-group $resourceGroup

# View app URLs
az webapp list --resource-group $resourceGroup --query "[].{Name:name, URL:defaultHostName}"

# Restart app
az webapp restart --resource-group $resourceGroup --name $apiAppName

# Stream logs
az webapp log tail --resource-group $resourceGroup --name $apiAppName

# SSH into app
az webapp remote-connection create --resource-group $resourceGroup --name $apiAppName

# Check SQL Server details
az sql server show --name $sqlServer --resource-group $resourceGroup

# List SQL databases
az sql db list --server $sqlServer --resource-group $resourceGroup
```

---

## Support & Resources

- Azure App Service: https://learn.microsoft.com/en-us/azure/app-service/
- Azure SQL Database: https://learn.microsoft.com/en-us/azure/azure-sql/database/
- Entity Framework Core: https://learn.microsoft.com/en-us/ef/core/
- Azure CLI Reference: https://learn.microsoft.com/en-us/cli/azure/
- Pricing Calculator: https://azure.microsoft.com/en-us/pricing/calculator/

---

**Deployment Complete! Your application is now live on Azure.** 🎉
