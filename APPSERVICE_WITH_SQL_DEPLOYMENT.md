# Azure DevOps Build and Install Deployment Guide

**Install-only deployment to the existing on-premises server `LSAPCFGDEVBE.develop.fcbt`**

This deployment does **not** create or use Azure App Service hosting. Azure is used only for the Azure DevOps project, pipeline, artifact storage, and VM Environment registration. The application is installed on the existing Windows server.

## Deployment architecture

```text
Git repository
    |
    v
Azure DevOps Pipeline
    |  Build API + frontend
    |  Publish config-system artifact
    v
Azure DevOps Environment: LSAPCFGDEVBE.develop.fcbt
    |
    v
Existing Windows server
    |-- ConfigSystem.Api Windows service
    |-- Frontend web server / reverse proxy
    `-- Existing on-premises SQL Server: LSAPCFGDEVBE.develop.fcbt
```

## Prerequisites

- Azure DevOps project with permission to create pipelines and environments.
- Administrative access to the target Windows server.
- Existing on-premises SQL Server and database provisioned by the DBA.
- The target server can reach SQL Server on the approved SQL port, normally `1433`.
- .NET 10 runtime or self-contained deployment requirements agreed with the server owner.
- A web server such as IIS or Nginx configured to serve the frontend and proxy only `/api` to `http://127.0.0.1:5000`.

The SQL Server must not be exposed to the public internet. Credentials and production settings stay on the target server or in an approved secret store and must not be committed to Git.

## Step 1: Create the Azure DevOps deployment resource

1. Open the Azure DevOps project.
2. Go to **Pipelines > Environments**.
3. Create an environment named exactly:

   ```text
   LSAPCFGDEVBE.develop.fcbt
   ```

4. Add a **Virtual Machine** resource to that environment.
5. Run the generated registration script on `LSAPCFGDEVBE.develop.fcbt` as the account that will run the deployment agent.
6. Grant that account permission to:
   - stop and start the `ConfigSystem.Api` Windows service;
   - write to `C:\Apps\ConfigSystem\api`;
   - write to `C:\Apps\ConfigSystem\frontend`.
7. Enable environment approvals or checks if production promotion requires approval.

The VM resource is the only Azure deployment resource required for this install-only model. Do not create an App Service, App Service Plan, Azure SQL Server, or Azure SQL Database for this deployment.

## Step 2: Prepare the target server once

Create the installation folders:

```powershell
New-Item -ItemType Directory -Force C:\Apps\ConfigSystem\api
New-Item -ItemType Directory -Force C:\Apps\ConfigSystem\frontend
New-Item -ItemType Directory -Force C:\Apps\ConfigSystem\api\dp-keys
```

Create the `ConfigSystem.Api` Windows service. The executable path must match the API installation path:

```powershell
New-Service `
  -Name ConfigSystem.Api `
  -BinaryPathName 'C:\Apps\ConfigSystem\api\ConfigSystem.Api.exe' `
  -DisplayName 'Config System API' `
  -StartupType Automatic
```

Configure the service with the production environment and loopback binding. Use the service manager or the organization-approved service wrapper to set these values:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5000
```

Do not open inbound firewall access to port `5000`. The frontend web server is the only public entry point and proxies `/api` locally.

## Step 3: Configure the existing SQL Server

The database is already provisioned on-premises. Do not run Azure SQL provisioning commands.

Use the approved production connection string mechanism on the target server, for example an environment variable or protected `appsettings.Production.json` file:

```text
Server=LSAPCFGDEVBE.develop.fcbt,1433;Initial Catalog=ConfigSystem;User ID=<secure-user>;Password=<secure-password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Also configure the required IBM i and GoAnywhere settings through the protected production configuration. Never place passwords, API keys, Data Protection keys, or production settings in the repository.

Before the first install, verify connectivity from the target server:

```powershell
Resolve-DnsName LSAPCFGDEVBE.develop.fcbt
Test-NetConnection LSAPCFGDEVBE.develop.fcbt -Port 1433
```

The current checked-in API still contains SQLite provider wiring for local development and seeding. Complete and validate the production SQL Server provider and migration work before using the SQL Server connection string in production. App Service settings are not involved in this install-only deployment.

## Step 4: Configure the Azure DevOps pipeline

The repository pipeline is [azure-pipelines.yml](azure-pipelines.yml). It:

1. builds the .NET API;
2. builds the Vite frontend;
3. publishes one `config-system` artifact;
4. downloads the artifact on the VM Environment agent; and
5. runs [scripts/Install-ConfigSystem.ps1](scripts/Install-ConfigSystem.ps1).

The deployment stage runs only after a successful build from `main` and installs to the environment named `LSAPCFGDEVBE.develop.fcbt`.

The default pipeline variables are:

```text
apiInstallPath: C:\Apps\ConfigSystem\api
frontendInstallPath: C:\Apps\ConfigSystem\frontend
apiServiceName: ConfigSystem.Api
```

Change these only in the pipeline if the target server uses different paths or a different service name.

## Step 5: Preserve server-only configuration

The installer mirrors the build artifact but deliberately preserves:

- `appsettings.Production.json`;
- `dp-keys`;
- SQLite database files, if still required by a local development deployment; and
- other server-only configuration excluded by the installer.

The installer stops the API service, updates the API and frontend files, and starts the service again. It does not create databases or overwrite production secrets.

## Step 6: Verify an installation

On the target server:

```powershell
Get-Service ConfigSystem.Api
Test-NetConnection 127.0.0.1 -Port 5000
```

Verify through the frontend web-server URL:

- the login page loads;
- login succeeds with the approved IBM i credentials;
- GoAnywhere Projects, Parameter Editor, and Audit Log load;
- API requests are sent through the frontend `/api` proxy; and
- port `5000` is not reachable from another machine.

Review the Azure DevOps deployment job and the Windows service event/application logs if the installation fails.

## Security requirements

- Do not create public Azure App Service endpoints for this application.
- Do not expose the API listener beyond `127.0.0.1`.
- Do not commit `.env`, production settings, passwords, API keys, or `dp-keys`.
- Rotate any Data Protection key or API credential that was previously committed.
- Keep SQL Server access restricted to the approved on-premises network and service identity.
- Configure Azure DevOps environment approvals before production installation.

## Related files

- [azure-pipelines.yml](azure-pipelines.yml)
- [DEVOPS_PIPELINE.md](DEVOPS_PIPELINE.md)
- [scripts/Install-ConfigSystem.ps1](scripts/Install-ConfigSystem.ps1)
- [frontend/nginx.conf](frontend/nginx.conf)
