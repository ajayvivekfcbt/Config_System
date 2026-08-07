# 🚀 Quick Start - Deploy to Azure in 3 Steps

**Get your Config System running on Azure with SQL Server in minutes**

---

## Step 1️⃣ Prepare (2 minutes)

### Install Tools (if needed)
```powershell
# Check if already installed
az --version           # Azure CLI
dotnet --version      # .NET 10
npm --version         # Node.js 18+

# If missing, install from:
# - Azure CLI: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli
# - .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0
# - Node.js: https://nodejs.org/
```

### Create Environment File
```powershell
# Copy template to .env
Copy-Item .env.example .env

# Edit .env with SQL Server credentials
notepad .env

# Update these values:
SQL_SERVER_NAME=configsystem-sql-1234
SQL_USER=sqladmin
SQL_PASSWORD=YourSecurePassword123!  # 8+ chars, mixed case, numbers, symbols
```

### Login to Azure
```powershell
az login
# Opens browser for authentication
```

---

## Step 2️⃣ Deploy (12-15 minutes)

### Run One-Click Deployment
```powershell
# Windows
.\deploy-appservice-sql.bat

# OR Linux/Mac
./deploy-appservice.sh
```

This script automatically:
- ✅ Creates resource group
- ✅ Creates Azure SQL Server + 3 databases
- ✅ Creates App Service Plan (S1)
- ✅ Creates backend & frontend App Services
- ✅ Builds .NET backend
- ✅ Builds React frontend
- ✅ Deploys both applications
- ✅ Configures all settings
- ✅ Initializes databases

**Just wait... the script handles everything!**

---

## Step 3️⃣ Access (1 minute)

After deployment completes, you'll see:

```
=== Deployment Complete! ===

Backend API:   https://configsystem-api-prod.azurewebsites.net
Frontend Web:  https://configsystem-web-prod.azurewebsites.net
API Docs:      https://configsystem-api-prod.azurewebsites.net/swagger

SQL Server:    configsystem-sql-XXXX.database.windows.net
Admin User:    sqladmin
```

### Try It Out
```powershell
# Test API is running
curl https://configsystem-api-prod.azurewebsites.net/swagger

# Open frontend in browser
Start-Process https://configsystem-web-prod.azurewebsites.net
```

---

## 💰 Monthly Cost

| Component | Cost |
|-----------|------|
| App Service Plan S1 | $50 |
| Azure SQL Database | $15 |
| Storage/Backup | $5 |
| **Total** | **$70/month** |

*Or use free tier: F1 App Service + Free SQL (~$15/month)*

---

## 📊 What Was Created

```
Azure Account
├── Resource Group: config-system-rg
├── SQL Server: configsystem-sql-XXXX
│   ├── Database: ConfigSystem (production)
│   ├── Database: ConfigSystem_Dev (development)
│   └── Database: ConfigSystem_Fcb (FCB staging)
├── App Service Plan: config-system-plan
├── App Service: configsystem-api-prod (backend)
│   ├── Runtime: .NET 10
│   └── Uses: ConfigSystem database
└── App Service: configsystem-web-prod (frontend)
    ├── Runtime: Node.js 20
    └── API URL: https://configsystem-api-prod...
```

---

## 🛠️ Useful Commands

### View Your Application
```powershell
# Get app URLs
az webapp list --resource-group config-system-rg --query "[].{Name:name, URL:defaultHostName}"

# Stream live logs
az webapp log tail --resource-group config-system-rg --name configsystem-api-prod

# Restart app
az webapp restart --resource-group config-system-rg --name configsystem-api-prod
```

### Manage Database
```powershell
# Connect to SQL Database
# Use: SQL Server Management Studio or Azure Data Studio
# Server: configsystem-sql-XXXX.database.windows.net
# Auth: SQL Login
# Username: sqladmin
# Password: (from .env)

# List databases
az sql db list --resource-group config-system-rg --server configsystem-sql-XXXX

# Query the database
# See APPSERVICE_WITH_SQL_DEPLOYMENT.md > Troubleshooting
```

---

## 🔗 Access Points

Once deployed, you can access:

| Component | URL |
|-----------|-----|
| **Frontend** | https://configsystem-web-prod.azurewebsites.net |
| **Backend API** | https://configsystem-api-prod.azurewebsites.net |
| **Swagger Docs** | https://configsystem-api-prod.azurewebsites.net/swagger |
| **SQL Database** | configsystem-sql-XXXX.database.windows.net:1433 |

---

## 🎯 Next Steps (Optional)

### 1. Set Up Custom Domain
```powershell
# Add your own domain name
# See APPSERVICE_WITH_SQL_DEPLOYMENT.md > Step 6
```

### 2. Enable Monitoring
```powershell
# Set up Application Insights for insights and alerts
# See APPSERVICE_WITH_SQL_DEPLOYMENT.md > Step 7
```

### 3. Configure Auto-Scaling
```powershell
# Scale out to multiple instances for high traffic
# See APPSERVICE_WITH_SQL_DEPLOYMENT.md > Scaling
```

### 4. Set Up Continuous Deployment
```powershell
# Deploy updates automatically when code is pushed
# See APPSERVICE_WITH_SQL_DEPLOYMENT.md
```

---

## ⚠️ Important Notes

1. **First Deployment**: Takes 10-15 minutes (deployment script shows progress)
2. **Database Initialization**: Automatic on first startup (check logs)
3. **CSV Data**: Imported from `backend/ConfigSystem.Api/data/` folder
4. **Backups**: SQL Database auto-backs up to 35 days
5. **SSL/HTTPS**: Automatic - no additional setup needed
6. **Costs**: Monitor Azure Portal to track spending

---

## 🚨 Troubleshooting

### Deployment Failed
```powershell
# View error details
az webapp log tail --resource-group config-system-rg --name configsystem-api-prod
```

### Can't Connect to Database
```powershell
# Check SQL firewall allows Azure services
az sql server firewall-rule list --resource-group config-system-rg --server configsystem-sql-XXXX

# Verify credentials in app settings
az webapp config appsettings list --resource-group config-system-rg --name configsystem-api-prod
```

### Frontend Can't Reach API
```powershell
# Verify API URL in frontend settings
az webapp config appsettings list --resource-group config-system-rg --name configsystem-web-prod | grep REACT_APP

# Test API directly
curl https://configsystem-api-prod.azurewebsites.net/health
```

**For more details:** See [APPSERVICE_WITH_SQL_DEPLOYMENT.md](APPSERVICE_WITH_SQL_DEPLOYMENT.md) > Troubleshooting

---

## 📚 Full Documentation

For detailed information, see:

- **[APPSERVICE_WITH_SQL_DEPLOYMENT.md](APPSERVICE_WITH_SQL_DEPLOYMENT.md)** - Complete 8-step guide
- **[SQL_SERVER_SETUP.md](SQL_SERVER_SETUP.md)** - SQL Server configuration
- **[VALIDATION_CHECKLIST.md](VALIDATION_CHECKLIST.md)** - Pre-deployment checklist
- **[DEPLOYMENT_READY.md](DEPLOYMENT_READY.md)** - Status overview

---

## ✨ You're Ready!

### To Deploy Now:

```powershell
# Make sure you're in the project root directory
cd c:\Users\apillai0418\Desktop\Config_System

# Run deployment
.\deploy-appservice-sql.bat

# Watch the progress...
# ~15 minutes later, your app is live! 🎉
```

---

## Questions?

- **Deployment issues?** → See APPSERVICE_WITH_SQL_DEPLOYMENT.md > Troubleshooting
- **Cost questions?** → See Cost Breakdown section above
- **Architecture questions?** → See What Was Created section above
- **Next steps?** → See Next Steps (Optional) section above

---

**Ready? Let's deploy! 🚀**

```powershell
.\deploy-appservice-sql.bat
```

*Your Config System will be running on Azure in about 15 minutes.*

---

**After Deployment:**
1. Test the API: https://configsystem-api-prod.azurewebsites.net/swagger
2. Access the UI: https://configsystem-web-prod.azurewebsites.net
3. Monitor logs: `az webapp log tail --resource-group config-system-rg --name configsystem-api-prod`
4. Review costs in Azure Portal

**Happy deploying! 🎉**
