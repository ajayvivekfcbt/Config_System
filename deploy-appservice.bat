@echo off
REM Quick deployment to Azure App Service
setlocal enabledelayedexpansion

echo === Config System - Azure App Service Deployment ===
echo.

REM Check if Azure CLI is installed
az --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Azure CLI not found. Install from https://learn.microsoft.com/en-us/cli/azure/install-azure-cli
    exit /b 1
)

REM Check if logged in
az account show >nul 2>&1
if errorlevel 1 (
    echo Logging in to Azure...
    az login
    if errorlevel 1 (
        echo ERROR: Failed to login to Azure
        exit /b 1
    )
)

echo.
echo [Step 1] Creating Azure resources...
echo.

set resourceGroup=config-system-rg
set location=eastus
set appServicePlan=config-system-plan
set backendName=configsystem-api-prod
set frontendName=configsystem-web-prod

REM Create resource group
echo Creating resource group: %resourceGroup%
az group create --name %resourceGroup% --location %location%
if errorlevel 1 exit /b 1

REM Create App Service Plan
echo Creating App Service Plan: %appServicePlan%
az appservice plan create ^
  --name %appServicePlan% ^
  --resource-group %resourceGroup% ^
  --sku S1 ^
  --is-linux
if errorlevel 1 exit /b 1

REM Create API App Service
echo Creating App Service for backend: %backendName%
az webapp create ^
  --resource-group %resourceGroup% ^
  --plan %appServicePlan% ^
  --name %backendName% ^
  --runtime "DOTNETCORE|10.0"
if errorlevel 1 exit /b 1

REM Create Frontend App Service
echo Creating App Service for frontend: %frontendName%
az webapp create ^
  --resource-group %resourceGroup% ^
  --plan %appServicePlan% ^
  --name %frontendName% ^
  --runtime "NODE|20"
if errorlevel 1 exit /b 1

echo.
echo [Step 2] Building and deploying backend...
echo.

cd backend\ConfigSystem.Api
if errorlevel 1 (
    echo ERROR: Cannot find backend directory
    exit /b 1
)

REM Build backend
echo Building .NET application...
dotnet publish -c Release -o ./publish
if errorlevel 1 (
    echo ERROR: Build failed
    exit /b 1
)

REM Zip and deploy
echo Packaging application...
powershell -Command "Compress-Archive -Path ./publish/* -DestinationPath app.zip -Force"

echo Deploying to App Service...
az webapp deployment source config-zip ^
  --resource-group %resourceGroup% ^
  --name %backendName% ^
  --src-path app.zip
if errorlevel 1 (
    echo ERROR: Deployment failed
    exit /b 1
)

REM Configure settings
echo Configuring app settings...
az webapp config appsettings set ^
  --resource-group %resourceGroup% ^
  --name %backendName% ^
  --settings ASPNETCORE_ENVIRONMENT=Production

cd ..\..

echo.
echo [Step 3] Building and deploying frontend...
echo.

cd frontend
if errorlevel 1 (
    echo ERROR: Cannot find frontend directory
    exit /b 1
)

REM Install and build
echo Installing dependencies...
call npm install
if errorlevel 1 (
    echo ERROR: npm install failed
    exit /b 1
)

echo Building React application...
call npm run build
if errorlevel 1 (
    echo ERROR: Build failed
    exit /b 1
)

REM Zip and deploy
echo Packaging application...
powershell -Command "Compress-Archive -Path ./dist/* -DestinationPath frontend.zip -Force"

echo Deploying to App Service...
az webapp deployment source config-zip ^
  --resource-group %resourceGroup% ^
  --name %frontendName% ^
  --src-path frontend.zip
if errorlevel 1 (
    echo ERROR: Deployment failed
    exit /b 1
)

REM Configure API URL
echo Configuring app settings...
az webapp config appsettings set ^
  --resource-group %resourceGroup% ^
  --name %frontendName% ^
  --settings REACT_APP_API_URL=https://%backendName%.azurewebsites.net

cd ..

echo.
echo === Deployment Complete! ===
echo.
echo Access your application:
echo.
echo Backend API:   https://%backendName%.azurewebsites.net
echo Frontend Web:  https://%frontendName%.azurewebsites.net
echo.
echo Next steps:
echo   1. Open https://%frontendName%.azurewebsites.net in your browser
echo   2. Test API at https://%backendName%.azurewebsites.net/swagger
echo   3. View logs: az webapp log tail --resource-group %resourceGroup% --name %backendName%
echo   4. Configure custom domain (optional)
echo.
echo Resource group: %resourceGroup%
echo To delete everything: az group delete --resource-group %resourceGroup%
echo.
pause
