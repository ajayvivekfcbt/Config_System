@echo off
REM Quick deployment to Azure App Service with SQL Database
setlocal enabledelayedexpansion

echo === Config System - Azure App Service + SQL Database Deployment ===
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
echo [Step 1] Setting up variables...
echo.

set resourceGroup=config-system-rg
set location=eastus
set appServicePlan=config-system-plan
set sqlServer=configsystem-sql-%random%
set sqlUser=%SQL_USER%
set sqlPassword=%SQL_PASSWORD%
set backendName=configsystem-api-prod
set frontendName=configsystem-web-prod

echo Resource Group: %resourceGroup%
echo SQL Server: %sqlServer%
echo.

if "%sqlUser%"=="" (
  echo ERROR: Set SQL_USER in the environment before running this legacy script.
  exit /b 1
)
if "%sqlPassword%"=="" (
  echo ERROR: Set SQL_PASSWORD in the environment before running this legacy script.
  exit /b 1
)

REM Create resource group
echo Creating resource group: %resourceGroup%
az group create --name %resourceGroup% --location %location%
if errorlevel 1 exit /b 1

REM Create SQL Server
echo.
echo [Step 2] Creating Azure SQL Server and databases...
echo.
echo Creating SQL Server: %sqlServer%
az sql server create ^
  --name %sqlServer% ^
  --resource-group %resourceGroup% ^
  --location %location% ^
  --admin-user %sqlUser% ^
  --admin-password %sqlPassword%
if errorlevel 1 exit /b 1

REM Allow Azure services to access SQL
echo Configuring SQL firewall for Azure services...
az sql server firewall-rule create ^
  --resource-group %resourceGroup% ^
  --server %sqlServer% ^
  --name AllowAzureServices ^
  --start-ip-address 0.0.0.0 ^
  --end-ip-address 0.0.0.0
if errorlevel 1 exit /b 1

REM Create databases
echo Creating ConfigSystem database...
az sql db create ^
  --server %sqlServer% ^
  --resource-group %resourceGroup% ^
  --name ConfigSystem ^
  --edition Basic
if errorlevel 1 exit /b 1

echo Creating ConfigSystem_Dev database...
az sql db create ^
  --server %sqlServer% ^
  --resource-group %resourceGroup% ^
  --name ConfigSystem_Dev ^
  --edition Basic
if errorlevel 1 exit /b 1

echo Creating ConfigSystem_Fcb database...
az sql db create ^
  --server %sqlServer% ^
  --resource-group %resourceGroup% ^
  --name ConfigSystem_Fcb ^
  --edition Basic
if errorlevel 1 exit /b 1

REM Create App Service Plan
echo.
echo [Step 3] Creating App Service Plan...
echo.
az appservice plan create ^
  --name %appServicePlan% ^
  --resource-group %resourceGroup% ^
  --sku S1 ^
  --is-linux
if errorlevel 1 exit /b 1

REM Create App Services
echo Creating backend App Service: %backendName%
az webapp create ^
  --resource-group %resourceGroup% ^
  --plan %appServicePlan% ^
  --name %backendName% ^
  --runtime "DOTNETCORE|10.0"
if errorlevel 1 exit /b 1

echo Creating frontend App Service: %frontendName%
az webapp create ^
  --resource-group %resourceGroup% ^
  --plan %appServicePlan% ^
  --name %frontendName% ^
  --runtime "NODE|20"
if errorlevel 1 exit /b 1

echo.
echo [Step 4] Building and deploying backend...
echo.

cd backend\ConfigSystem.Api
if errorlevel 1 (
    echo ERROR: Cannot find backend directory
    exit /b 1
)

echo Building .NET application...
dotnet publish -c Release -o ./publish
if errorlevel 1 (
    echo ERROR: Build failed
    exit /b 1
)

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

echo Configuring app settings...
az webapp config appsettings set ^
  --resource-group %resourceGroup% ^
  --name %backendName% ^
  --settings ASPNETCORE_ENVIRONMENT=Production

az webapp config appsettings set ^
  --resource-group %resourceGroup% ^
  --name %backendName% ^
  --settings ^
    "SQL_SERVER_NAME=%sqlServer%" ^
    "SQL_USER=%sqlUser%" ^
    "SQL_PASSWORD=%sqlPassword%"

cd ..\..

echo.
echo [Step 5] Building and deploying frontend...
echo.

cd frontend
if errorlevel 1 (
    echo ERROR: Cannot find frontend directory
    exit /b 1
)

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
echo API Docs:      https://%backendName%.azurewebsites.net/swagger
echo.
echo SQL Server:    %sqlServer%.database.windows.net
echo Admin User:    %sqlUser%
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
