# GoAnywhere Configuration System

Centralized management system for **GoAnywhere projects** and their **configuration parameters** across multiple environments (DATO, DATI, DATU, DATV, DATN, FCB).

Built with **.NET Core API + React UI** providing a web-based interface to manage project configurations, execute projects, and track execution details.

| Folder | Contents |
|--------|----------|
| `backend/` | ASP.NET Core (.NET 10) Web API + EF Core (SQLite) |
| `frontend/` | React 18 + Vite + TypeScript UI |

## Quick Start

```text
# Run the backend from backend/ConfigSystem.Api:
cd backend/ConfigSystem.Api
dotnet run

# Run the frontend from frontend/:
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
- **Variable Management**: Build and pass configuration variables to GoAnywhere at runtime
- **Project Path Support**: Track and manage project folder paths on the GoAnywhere server
- **Execution Tracking**: Log and track project execution results

### Data Import/Seeding
- **CSV Support**: Import projects from CSV files (e.g., `UTCFGXTN.csv`)
- **XML Support**: Load GoAnywhere project definitions from embedded XML
- **Multi-Source**: Support for both CSV and XML project sources

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

