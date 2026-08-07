# Azure App Service Deployment Guide

**Simple, low-maintenance deployment for low-usage applications**

## Overview

This guide deploys your Config System directly to Azure App Service without Docker, containers, or database servers. Perfect for internal, low-traffic applications.

### What You Get
- ✅ Zero database administration (SQLite embedded)
- ✅ Automatic scaling for traffic spikes
- ✅ Built-in monitoring and diagnostics
- ✅ Free SSL/TLS certificates
- ✅ Simple deployment (push code, it runs)
- ✅ ~$50-100/month for small apps

---

## Architecture

```
┌──────────────────────────────────────────────────┐
│         Azure App Service                         │
│  ┌─────────────────────────────────────────────┐ │
│  │  Config System API (.NET 10)                │ │
│  │  + SQLite Database (embedded)               │ │
│  └─────────────────────────────────────────────┘ │
│                                                  │
│  + Static Files (React Frontend)                │
│  + Automatic Scaling                             │
│  + Free SSL Certificate                          │
└──────────────────────────────────────────────────┘

┌──────────────────────────────┐
│   Azure Storage (Optional)    │
│   - Backup SQLite databases  │
│   - Store uploaded files      │
└──────────────────────────────┘
```

---

## Prerequisites

1. **Azure Account**
   - Sign up: https://azure.microsoft.com/free
   - Free trial credits: $200

2. **Local Tools**
   - .NET 10 SDK: https://dotnet.microsoft.com/download
   - Azure CLI: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli
   - Git

3. **Verify Installation**
   ```powershell
   dotnet --version
   az --version
   git --version
   ```

---

## Step 1: Prepare Application

### 1.1 Build Locally
```powershell
cd backend/ConfigSystem.Api
dotnet restore
dotnet build -c Release
```

### 1.2 Test Locally
```powershell
dotnet run
# Open http://localhost:5000/swagger
```

### 1.3 Publish for Deployment
```powershell
dotnet publish -c Release -o ./publish
```

This creates a `publish` folder with everything needed to run on App Service.

---

## Step 2: Deploy Backend to App Service

### 2.1 Login to Azure
```powershell
az login
# Opens browser to authenticate
```

### 2.2 Create Resource Group
```powershell
$resourceGroup = "config-system-rg"
$location = "eastus"  # Change to your region

az group create --name $resourceGroup --location $location
```

### 2.3 Create App Service Plan
```powershell
$appServicePlan = "config-system-plan"

# For low usage: Standard S1 plan (~$50/month)
az appservice plan create `
  --name $appServicePlan `
  --resource-group $resourceGroup `
  --sku S1 `
  --is-linux
```

### 2.4 Create App Service for Backend
```powershell
$appServiceName = "configsystem-api-prod"  # Must be globally unique

az webapp create `
  --resource-group $resourceGroup `
  --plan $appServicePlan `
  --name $appServiceName `
  --runtime "DOTNETCORE|10.0"
```

### 2.5 Deploy Code
```powershell
# From backend/ConfigSystem.Api directory
dotnet publish -c Release -o ./publish

# Deploy using ZIP
Compress-Archive -Path ./publish/* -DestinationPath app.zip
az webapp deployment source config-zip `
  --resource-group $resourceGroup `
  --name $appServiceName `
  --src-path app.zip
```

**Or use Visual Studio deployment:**
- Right-click project → Publish → Azure App Service
- Follow the wizard

### 2.6 Configure Application Settings
```powershell
# Set environment to Production
az webapp config appsettings set `
  --resource-group $resourceGroup `
  --name $appServiceName `
  --settings ASPNETCORE_ENVIRONMENT=Production

# Set IBM i connection info (if needed)
az webapp config appsettings set `
  --resource-group $resourceGroup `
  --name $appServiceName `
  --settings `
    "Ibmi__System=10.10.1.55" `
    "Fcb400__System=10.10.1.52" `
    "Fcb400__Library=UTPRODD" `
    "Fcb400__Uid=username" `
    "Fcb400__Pwd=password"
```

### 2.7 Verify Backend is Running
```powershell
# Get the URL
$backendUrl = "https://$appServiceName.azurewebsites.net"
Write-Host "Backend URL: $backendUrl"

# Test health
curl "$backendUrl/health"
```

---

## Step 3: Deploy Frontend to App Service

### 3.1 Build React Frontend
```powershell
cd frontend
npm install
npm run build
# Creates dist/ folder with optimized files
```

### 3.2 Create Separate App Service for Frontend
```powershell
$frontendName = "configsystem-web-prod"

az webapp create `
  --resource-group $resourceGroup `
  --plan $appServicePlan `
  --name $frontendName `
  --runtime "NODE|20"
```

### 3.3 Deploy Frontend
```powershell
cd frontend
npm install
npm run build

# Deploy the dist folder
Compress-Archive -Path ./dist/* -DestinationPath frontend.zip
az webapp deployment source config-zip `
  --resource-group $resourceGroup `
  --name $frontendName `
  --src-path frontend.zip
```

### 3.4 Configure Frontend to Use Backend API
```powershell
# Set API URL to backend App Service
az webapp config appsettings set `
  --resource-group $resourceGroup `
  --name $frontendName `
  --settings `
    "REACT_APP_API_URL=https://$appServiceName.azurewebsites.net"
```

### 3.5 Verify Frontend
```powershell
$frontendUrl = "https://$frontendName.azurewebsites.net"
Write-Host "Frontend URL: $frontendUrl"

# Open in browser
Start-Process $frontendUrl
```

---

## Step 4: Connect Frontend to Backend

Update frontend API calls to use production URL:

### Option A: Environment Variable (Recommended)
Edit [frontend/src/api.ts](../frontend/src/api.ts):

```typescript
const API_URL = process.env.REACT_APP_API_URL || '/api';

export async function fetchConfigs() {
  const response = await fetch(`${API_URL}/api/configs`);
  // ...
}
```

### Option B: Hardcoded URL
```typescript
const API_URL = 'https://configsystem-api-prod.azurewebsites.net';
```

Rebuild and redeploy:
```powershell
npm run build
Compress-Archive -Path ./dist/* -DestinationPath frontend.zip
az webapp deployment source config-zip `
  --resource-group $resourceGroup `
  --name $frontendName `
  --src-path frontend.zip
```

---

## Step 5: Enable HTTPS & Custom Domain

### 5.1 Enable HTTPS (Automatic)
Azure App Service provides free SSL certificates by default. Just access via `https://`

### 5.2 Custom Domain (Optional)
```powershell
# Map your domain
az webapp config hostname add `
  --resource-group $resourceGroup `
  --webapp-name $appServiceName `
  --hostname "api.yourdomain.com"

# Update DNS record at your registrar
# CNAME: api.yourdomain.com → configsystem-api-prod.azurewebsites.net
```

---

## Step 6: Backup & Data Persistence

### 6.1 Backup SQLite Database
```powershell
# Connect via Kudu (advanced console)
$kuduUrl = "https://$appServiceName.scm.azurewebsites.net"

# Or download via Azure Portal:
# App Service → Development Tools → Kudu → File Browser
# Navigate to /home/site/wwwroot
# Download *.db files
```

### 6.2 Automated Backup to Azure Storage
```powershell
# Create storage account
$storageAccount = "configstorage$(Get-Random)"
az storage account create `
  --resource-group $resourceGroup `
  --name $storageAccount `
  --sku Standard_LRS

# Enable backup in App Service
az webapp config backup update `
  --resource-group $resourceGroup `
  --webapp-name $appServiceName `
  --storage-account-url "https://$storageAccount.blob.core.windows.net" `
  --backup-frequency "Daily" `
  --retention 7
```

---

## Step 7: Monitor & Troubleshoot

### 7.1 View Logs
```powershell
# Real-time logs
az webapp log tail `
  --resource-group $resourceGroup `
  --name $appServiceName

# Or in Azure Portal:
# App Service → App Service Logs
```

### 7.2 Application Insights (Optional)
```powershell
# Create Application Insights
az monitor app-insights component create `
  --resource-group $resourceGroup `
  --app config-system-insights

# Connect to App Service (via Portal)
# App Service → Application Insights → Connect
```

### 7.3 Common Issues

| Issue | Solution |
|-------|----------|
| 500 errors | Check logs: `az webapp log tail` |
| Database locked | SQLite limitation; consider SQL Server if growth occurs |
| Slow performance | Scale up plan: `az appservice plan update --sku P1V2` |
| CORS errors | Check `appsettings.Production.json` CORS settings |

---

## Step 8: Scaling & Performance

### 8.1 Auto-Scale (Optional)
```powershell
# Enable auto-scale for traffic spikes
az monitor autoscale create `
  --resource-group $resourceGroup `
  --resource-name $appServicePlan `
  --resource-type "Microsoft.Web/serverfarms" `
  --min-count 1 `
  --max-count 3 `
  --count 1
```

### 8.2 Upgrade Plan if Needed
```powershell
# Start with S1, upgrade if needed
az appservice plan update `
  --resource-group $resourceGroup `
  --name $appServicePlan `
  --sku "P1V2"  # Premium tier
```

---

## Step 9: Continuous Deployment (Optional)

### 9.1 GitHub Integration
```powershell
# Connect to GitHub repo
az webapp deployment github-actions add `
  --resource-group $resourceGroup `
  --name $appServiceName `
  --repo "yourusername/configsystem" `
  --branch main
```

### 9.2 GitHub Actions Workflow
Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure App Service

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Build
        run: dotnet publish -c Release -o ./publish backend/ConfigSystem.Api
      
      - name: Deploy
        uses: azure/webapps-deploy@v2
        with:
          app-name: configsystem-api-prod
          publish-profile: ${{ secrets.AZURE_PUBLISH_PROFILE }}
          package: ./publish
```

---

## Cost Estimation

| Service | Tier | Cost/Month |
|---------|------|-----------|
| App Service Plan | S1 | $50 |
| App Service (API) | S1 | Included |
| App Service (Frontend) | S1 | Included |
| Storage (backup) | Standard LRS | $5-10 |
| Application Insights | Pay-as-you-go | $0-10 |
| **Total** | | **~$60-70/month** |

For low usage (< 50 GB data, < 1000 requests/day):
- Free tier available for testing
- F1 tier: $0/month (with limitations)
- B1: $12/month (first tier with no limits)

---

## Maintenance

### Weekly
- Monitor application logs
- Check error rates in Application Insights

### Monthly
- Review costs in Azure Cost Management
- Update dependencies: `dotnet outdated`
- Backup databases manually if needed

### Quarterly
- Review App Service plan size
- Update .NET runtime if new version available
- Review security posture

---

## Disaster Recovery

### Backup Plan
```powershell
# Manual backup before major changes
az webapp deployment slot create `
  --resource-group $resourceGroup `
  --name $appServiceName `
  --slot staging

# Test in staging
# Swap to production when ready
az webapp deployment slot swap `
  --resource-group $resourceGroup `
  --name $appServiceName `
  --slot staging
```

### Restore from Backup
```powershell
# List available backups
az webapp config backup list `
  --resource-group $resourceGroup `
  --webapp-name $appServiceName

# Restore
az webapp config backup restore `
  --resource-group $resourceGroup `
  --webapp-name $appServiceName `
  --backup-id <backup-id>
```

---

## Useful Azure CLI Commands

```powershell
# View app details
az webapp show --resource-group $resourceGroup --name $appServiceName

# View app settings
az webapp config appsettings list --resource-group $resourceGroup --name $appServiceName

# Restart app
az webapp restart --resource-group $resourceGroup --name $appServiceName

# Stop app
az webapp stop --resource-group $resourceGroup --name $appServiceName

# Start app
az webapp start --resource-group $resourceGroup --name $appServiceName

# Delete everything
az group delete --resource-group $resourceGroup
```

---

## Next Steps

1. ✅ Deploy backend to App Service
2. ✅ Deploy frontend to App Service
3. ✅ Configure custom domain (optional)
4. ✅ Set up monitoring
5. ✅ Enable automated backups
6. ✅ Document access procedures for team

**Your app is now in production with minimal overhead!**

---

## Support & Resources

- Azure App Service Docs: https://learn.microsoft.com/en-us/azure/app-service/
- .NET on App Service: https://learn.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore
- Azure CLI Reference: https://learn.microsoft.com/en-us/cli/azure/
- Pricing: https://azure.microsoft.com/en-us/pricing/details/app-service/
