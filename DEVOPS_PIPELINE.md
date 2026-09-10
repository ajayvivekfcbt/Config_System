# Azure DevOps Pipeline

`azure-pipelines.yml` builds the .NET API and Vite frontend, publishes a single `config-system` artifact, and installs it to `LSAPCFGDEVBE.develop.fcbt` after a successful `main` build.

## One-time setup

1. Create an Azure DevOps Environment named `LSAPCFGDEVBE.develop.fcbt`.
2. Add the target server as a Virtual Machine resource. The agent must run as an account that can stop and start the `ConfigSystem.Api` Windows service and write to the installation folders.
3. Create the `ConfigSystem.Api` Windows service before the first deployment, pointing to `C:\Apps\ConfigSystem\api\ConfigSystem.Api.exe`.
4. Keep environment-specific files on the server: `appsettings.Production.json`, `dp-keys`, and SQLite `*.db` files. The installer deliberately preserves them.
5. In the pipeline Variables page, change `apiInstallPath`, `frontendInstallPath`, and `apiServiceName` when the server uses different values.

## API isolation

The API must not be published directly to the network. Configure the `ConfigSystem.Api` Windows service with `ASPNETCORE_URLS=http://127.0.0.1:5000`, and configure the web server that hosts `C:\Apps\ConfigSystem\frontend` to proxy only `/api` to `http://127.0.0.1:5000`. Do not add an inbound firewall rule for port `5000`.

All `/api` routes require the server-side IBM i authenticated session created by `/api/auth/login`; the two former external GoAnywhere parameter endpoints have been removed. Swagger is enabled only in Development.

Browser developer tools cannot be disabled for a web application: the browser necessarily receives the UI code and the configuration values a signed-in user is allowed to view. The production Nginx configuration prevents framing, restricts browser resource sources, and disables caching of API responses. Protect values through server-side authorization and by never placing credentials, API keys, or unmasked secrets in frontend build variables.

The deployment stage runs only for the `main` branch. Environment approvals can be enabled in Azure DevOps before the install job runs.