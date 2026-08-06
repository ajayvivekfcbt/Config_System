#!/bin/bash
# Quick deployment to Azure App Service (Linux/Mac)

set -e

echo "=== Config System - Azure App Service Deployment ==="
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "ERROR: Azure CLI not found. Install from https://learn.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check if logged in
if ! az account show &> /dev/null; then
    echo "Logging in to Azure..."
    az login
fi

echo ""
echo "[Step 1] Creating Azure resources..."
echo ""

resourceGroup="config-system-rg"
location="eastus"
appServicePlan="config-system-plan"
backendName="configsystem-api-prod"
frontendName="configsystem-web-prod"

# Create resource group
echo "Creating resource group: $resourceGroup"
az group create --name "$resourceGroup" --location "$location"

# Create App Service Plan
echo "Creating App Service Plan: $appServicePlan"
az appservice plan create \
  --name "$appServicePlan" \
  --resource-group "$resourceGroup" \
  --sku S1 \
  --is-linux

# Create API App Service
echo "Creating App Service for backend: $backendName"
az webapp create \
  --resource-group "$resourceGroup" \
  --plan "$appServicePlan" \
  --name "$backendName" \
  --runtime "DOTNETCORE|10.0"

# Create Frontend App Service
echo "Creating App Service for frontend: $frontendName"
az webapp create \
  --resource-group "$resourceGroup" \
  --plan "$appServicePlan" \
  --name "$frontendName" \
  --runtime "NODE|20"

echo ""
echo "[Step 2] Building and deploying backend..."
echo ""

cd backend/ConfigSystem.Api || { echo "ERROR: Cannot find backend directory"; exit 1; }

# Build backend
echo "Building .NET application..."
dotnet publish -c Release -o ./publish

# Zip and deploy
echo "Packaging application..."
zip -r app.zip publish

echo "Deploying to App Service..."
az webapp deployment source config-zip \
  --resource-group "$resourceGroup" \
  --name "$backendName" \
  --src-path app.zip

# Configure settings
echo "Configuring app settings..."
az webapp config appsettings set \
  --resource-group "$resourceGroup" \
  --name "$backendName" \
  --settings ASPNETCORE_ENVIRONMENT=Production

cd ../..

echo ""
echo "[Step 3] Building and deploying frontend..."
echo ""

cd frontend || { echo "ERROR: Cannot find frontend directory"; exit 1; }

# Install and build
echo "Installing dependencies..."
npm install

echo "Building React application..."
npm run build

# Zip and deploy
echo "Packaging application..."
zip -r frontend.zip dist

echo "Deploying to App Service..."
az webapp deployment source config-zip \
  --resource-group "$resourceGroup" \
  --name "$frontendName" \
  --src-path frontend.zip

# Configure API URL
echo "Configuring app settings..."
az webapp config appsettings set \
  --resource-group "$resourceGroup" \
  --name "$frontendName" \
  --settings "REACT_APP_API_URL=https://$backendName.azurewebsites.net"

cd ..

echo ""
echo "=== Deployment Complete! ==="
echo ""
echo "Access your application:"
echo ""
echo "Backend API:   https://$backendName.azurewebsites.net"
echo "Frontend Web:  https://$frontendName.azurewebsites.net"
echo ""
echo "Next steps:"
echo "  1. Open https://$frontendName.azurewebsites.net in your browser"
echo "  2. Test API at https://$backendName.azurewebsites.net/swagger"
echo "  3. View logs: az webapp log tail --resource-group $resourceGroup --name $backendName"
echo "  4. Configure custom domain (optional)"
echo ""
echo "Resource group: $resourceGroup"
echo "To delete everything: az group delete --resource-group $resourceGroup"
echo ""
