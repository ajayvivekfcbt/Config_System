# GoAnywhere Configuration System

Centralized management system for **GoAnywhere projects** and their **configuration parameters** across multiple environments (DATO, DATI, DATU, DATV, DATN, FCB).

Built with **.NET Core API + React UI** providing a web-based interface to manage project configurations, execute projects, and track execution details.

| Folder | Contents |
|--------|----------|
| `backend/` | ASP.NET Core (.NET 10) Web API + EF Core (SQLite) |
| `frontend/` | React 18 + Vite + TypeScript UI |
| `IRS-Projects/` | GoAnywhere project XML definitions for IRS compliance workflows |

## Quick Start

```powershell
# Run both backend and frontend with:
.\start.ps1

# Or manually:

# 1. Backend  ->  http://localhost:5198  (Swagger at /swagger)
cd backend/ConfigSystem.Api
dotnet run

# 2. Frontend ->  http://localhost:5173  (proxies /api to the backend)
cd frontend
npm install
npm run dev
```

The database (`configsystem.db`) is created and seeded automatically on first run.

## Features

### Configuration Management
- **Project Management**: Create and manage GoAnywhere projects with metadata
- **Configuration Storage**: Store and organize configuration parameters by project and environment
- **Environment Support**: Six distinct environments (DATO, DATI, DATU, DATV, DATN, FCB)
- **Sensitive Data Masking**: Automatically mask sensitive configuration values in the UI

### Project Execution
- **PowerShell Integration**: Execute GoAnywhere projects via `Invoke-GoAnywhereProject.ps1`
- **Variable Management**: Build and pass configuration variables to GoAnywhere at runtime
- **Project Path Support**: Track and manage project folder paths on the GoAnywhere server
- **Execution Tracking**: Log and track project execution results

### Data Import/Seeding
- **CSV Support**: Import projects from CSV files (e.g., `UTCFGXTN.csv`)
- **XML Support**: Load GoAnywhere project definitions from embedded XML
- **Multi-Source**: Support for both CSV and XML project sources

## PowerShell Scripts

### `Invoke-GoAnywhereProject.ps1`
Execute a GoAnywhere project with configurations from ConfigSystem API.

```powershell
.\Invoke-GoAnywhereProject.ps1 `
  -ProjectName "APClearedChecks" `
  -Environment DATO `
  -GoAnywhereUrl "https://GOANYDEV.develop.fcbt:8001/goanywhere/rest/gacmd/v1/projects" `
  -ApiKey "your-api-key" `
  -ProjectPath "/dev/Ajay" `
  -SkipCertificateCheck
```

### `Get-ProjectConfiguration.ps1`
Retrieve configuration parameters for a project and environment.

```powershell
.\Get-ProjectConfiguration.ps1 `
  -ProjectName "TestAPI" `
  -Environment DATO `
  -Format JSON
```

## Database Schema

### GAPROJECT
Stores GoAnywhere project information:
- **Id**: Unique project identifier
- **Name**: Project name
- **Description**: Project description
- **ProjectPath**: Folder path on GoAnywhere server (editable)
- **ExtentId, ExtentName, ContextId**: Legacy reference fields

### GACONFIG
Stores configuration parameters by project and environment:
- **ProjectId**: Reference to GAPROJECT
- **Environment**: DATO, DATI, DATU, DATV, DATN, or FCB
- **ConfigKey**: Parameter name
- **ConfigValue**: Parameter value
- **IsRequired**: Flag for required parameters
- **IsSensitive**: Flag for sensitive data (masked in UI)

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/goanywhere/projects` | List all projects |
| GET | `/api/goanywhere/configs?projectId=X&environment=Y` | Get configurations for project/environment |
| PUT | `/api/goanywhere/configs/{id}` | Update configuration value |
| PUT | `/api/goanywhere/projects/{id}/path` | Update project path |
| GET | `/api/goanywhere/summary` | Get system summary statistics |

## IRS Projects

The `IRS-Projects/` folder contains XML definitions for IRS compliance workflows:

- **OFAC_Compliance.xml** - OFAC sanctions checking
- **IRS_941_PayrollTax.xml** - Quarterly payroll tax returns
- **IRS_1098_InterestReporting.xml** - Mortgage interest statements
- **IRS_W2_Submission.xml** - W-2 wage statements
- **IRS_1099_DividendReporting.xml** - Dividend reporting

These can be imported into ConfigSystem as project templates.
