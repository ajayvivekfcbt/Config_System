# GoAnywhere Configuration Promotion

This folder is the promotion boundary for the GoAnywhere Configuration Application.

- The frontend artifact is built from `../frontend`; its application shell imports only the GoAnywhere project, parameter editor, audit, login, and log-off components.
- The API publishes only controllers and routes available to the GoAnywhere application. IBM i CRUD, resolution, audit, and FCB staging mappings are excluded unless the `IBMI_CONFIGURATION` compilation symbol is explicitly defined.
- The deployment pipeline remains at `../azure-pipelines.yml` and installs the GoAnywhere-only application to `LSAPCFGDEVBE.develop.fcbt`.

IBM i source files remain in the repository for the separate application, but they are not reachable through the GoAnywhere deployment.