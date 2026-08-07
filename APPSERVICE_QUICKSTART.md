# Azure App Service Deployment - Quick Summary

**Simplified deployment approach with managed SQL Server database**

## What Changed?

✅ **Using SQL Server** - Azure SQL Database (managed service)  
✅ **No Docker** - Eliminated container complexity  
✅ **Simple Azure App Service** - Push code, it runs automatically
✅ **Lower maintenance** - SQL Server is managed by Azure
✅ **~$80-120/month** - Includes App Service + SQL Database

---

## 3-Step Deployment

### Step 1: Build Locally
```powershell
# Backend
cd backend/ConfigSystem.Api
dotnet publish -c Release

# Frontend
cd ../../frontend
npm install
npm run build
```

### Step 2: Create Azure Resources
```powershell
# Option A: Run automated script (easiest)
.\deploy-appservice.bat

# Option B: Manual commands
az group create --name config-system-rg --location eastus

# Create App Service Plan
az appservice plan create --name config-system-plan --resource-group config-system-rg --sku S1 --is-linux

# Create SQL Server
az sql server create --name configsystem-sql-server --resource-group config-system-rg --admin-user sqladmin --admin-password "YourPassword123!"

# Create SQL Database
az sql db create --server configsystem-sql-server --resource-group config-system-rg --name ConfigSystem

# Create App Services
az webapp create --resource-group config-system-rg --plan config-system-plan --name configsystem-api-prod --runtime "DOTNETCORE|10.0"
az webapp create --resource-group config-system-rg --plan config-system-plan --name configsystem-web-prod --runtime "NODE|20"
```

### Step 3: Deploy
```powershell
# Backend
Compress-Archive -Path backend/ConfigSystem.Api/publish/* -DestinationPath app.zip
az webapp deployment source config-zip --resource-group config-system-rg --name configsystem-api-prod --src-path app.zip

# Frontend
Compress-Archive -Path frontend/dist/* -DestinationPath frontend.zip
az webapp deployment source config-zip --resource-group config-system-rg --name configsystem-web-prod --src-path frontend.zip
```

---

## Your Application URLs

After deployment:
- **Backend API**: https://configsystem-api-prod.azurewebsites.net
- **Frontend**: https://configsystem-web-prod.azurewebsites.net
- **API Docs**: https://configsystem-api-prod.azurewebsites.net/swagger

---

## Key Files

| File | Purpose |
|------|---------|
| [APPSERVICE_DEPLOYMENT.md](APPSERVICE_DEPLOYMENT.md) | Full deployment guide with SQL Server setup |
| [deploy-appservice.bat](deploy-appservice.bat) | One-click deployment (Windows) |
| [deploy-appservice.sh](deploy-appservice.sh) | One-click deployment (Linux/Mac) |

---

## Cost

| Component | Cost |
|-----------|------|
| App Service Plan (S1) | $50/month |
| Azure SQL Database (Basic) | $15-20/month |
| Storage (optional backups) | $5-10/month |
| **Total** | **~$80-90/month** |

Free tier available for testing with limitations.

---

## What's New

✅ **Azure SQL Database** - Managed, scalable SQL Server  
✅ **Automatic backups** - Built into SQL Database  
✅ **High availability** - Geo-redundancy available  
✅ **Connection pooling** - Better performance  
✅ **Security** - Firewall rules, encryption at rest/in transit  

---

## Next Steps

1. ✅ Read [APPSERVICE_DEPLOYMENT.md](APPSERVICE_DEPLOYMENT.md) for detailed instructions
2. ✅ Run `./deploy-appservice.bat` (or `.sh` on Linux/Mac)
3. ✅ Configure app settings with SQL Server details
4. ✅ Access your app at the URLs above
5. ✅ Monitor in Azure Portal
6. ✅ Set up custom domain (optional)

---

**Questions? See [APPSERVICE_DEPLOYMENT.md](APPSERVICE_DEPLOYMENT.md) for complete guide.**
