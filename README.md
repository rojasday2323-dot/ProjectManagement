# ProjectManagement

Sistema web full stack para seguimiento y administración de proyectos.

## Stack Tecnológico

| Capa | Tecnología |
|------|------------|
| **Frontend** | React 19 + TypeScript 6 + Vite 8 |
| **Backend** | .NET 8 + ASP.NET Core Minimal API |
| **Base de datos** | SQLite + Entity Framework Core |
| **Autenticación** | Microsoft Entra ID + MSAL + JWT |
| **IA** | Claude via Azure AI Foundry |
| **Integraciones** | Azure DevOps REST API + WIQL + Git API |
| **Deployment** | IIS + PowerShell |
| **Tests** | xUnit + NSubstitute |

## Estructura del Proyecto

```
ProjectManagement/
├── src/
│   ├── ProjectManagement.Api/        # Backend API
│   └── ProjectManagement.Web/        # Frontend SPA
├── tests/
│   └── ProjectManagement.Api.Tests/  # Tests unitarios
├── deploy/                           # Scripts de despliegue
├── ProjectManagement.sln
└── README.md
```

## Requisitos Previos

- .NET 8 SDK
- Node.js 20+
- Git

## Inicio Rápido

### Backend

```bash
cd src/ProjectManagement.Api
dotnet run
```

### Frontend

```bash
cd src/ProjectManagement.Web
npm install
npm run dev
```

El frontend requiere la configuración de Microsoft Entra ID descrita en la sección **Autenticación y Azure DevOps**, salvo que se habilite explícitamente el modo local de desarrollo documentado abajo. Por defecto, sin Entra muestra un aviso y la API permanece cerrada.

Para desarrollo local solamente, existe un bypass de autenticación de activación explícita. Habilítalo en ambos procesos; no uses esta opción en una red compartida o en producción:

```powershell
# En src/ProjectManagement.Api
dotnet user-secrets set "Authentication:AllowAnonymousInDevelopment" "true"

# En src/ProjectManagement.Web/.env.local (archivo ignorado por Git)
VITE_DEV_AUTH_BYPASS=true
```

Reinicia API y Vite después de cambiar estos valores. El backend solo omite la política global de autenticación cuando el entorno es `Development` y la opción es `true`; producción mantiene la protección aunque la opción esté definida. La pantalla Azure DevOps conserva autorización explícita y requiere Entra/OBO, por lo que no funcionará con este bypass. Desactiva el modo local eliminando la clave con `dotnet user-secrets remove "Authentication:AllowAnonymousInDevelopment"` y quitando la variable del `.env.local`.

### Tests

```bash
dotnet test
```

## Análisis de documentos con Azure AI Foundry

El análisis usa la Messages API de Claude en Azure AI Foundry. La API envía únicamente el texto extraído del documento o de la transcripción, trata su contenido como dato no confiable y no lo escribe en logs. La respuesta se valida antes de guardarse; los hechos del resumen deben incluir evidencia literal del origen. Si el proveedor no está configurado o falla, el análisis queda marcado como fallido y no se presenta como exitoso.

En desarrollo, ejecuta desde `src/ProjectManagement.Api` y configura los secretos con User Secrets:

```powershell
dotnet user-secrets set "AzureAI:Endpoint" "https://<resource-name>.services.ai.azure.com/anthropic/v1/messages"
dotnet user-secrets set "AzureAI:DeploymentName" "<deployment-name>"
dotnet user-secrets set "AzureAI:ApiKey" "<API_KEY>"
dotnet user-secrets set "AzureAI:ApiVersion" "2023-06-01"
```

Sustituye los placeholders por valores que el administrador obtenga de su recurso y deployment de Foundry. `Endpoint` debe ser la URI de Messages API del recurso Claude; `DeploymentName` es el nombre del deployment y se envía al proveedor como `model`. Esta sección `AzureAI` también admite `ApiVersion`; si se omite, se usa `2023-06-01`. Se conserva compatibilidad con los nombres anteriores `AzureAiFoundry:Model` y `AzureAiFoundry:*`. La clave viaja únicamente del backend al proveedor mediante HTTPS. No guardes secretos en `appsettings.json`, archivos del frontend, SQLite ni logs.

En producción, inyecta las mismas opciones como variables de entorno desde el almacén de secretos del entorno (por ejemplo, Azure Key Vault), nunca desde el repositorio:

```powershell
$env:AzureAI__Endpoint = "https://<resource-name>.services.ai.azure.com/anthropic/v1/messages"
$env:AzureAI__DeploymentName = "<deployment-name>"
$env:AzureAI__ApiKey = "<API_KEY>"
$env:AzureAI__ApiVersion = "2023-06-01"
```

Rota la clave de acuerdo con la política de seguridad y actualiza el secreto desplegado sin incluirlo en artefactos de publicación. La pantalla consulta `GET /api/ai/status` y presenta las opciones que faltan sin devolver sus valores.

En **Documentos y notas**, abre un PDF, DOCX, TXT, nota o transcripción con texto preparado y selecciona **Analizar con IA**. El resultado contiene el resumen inteligente (resumen general, temas, acuerdos, decisiones, pendientes, responsables, fechas, compromisos, entregables, riesgos y próximos pasos) y propuestas revisables. Los responsables y fechas ambiguos se señalan para confirmar y no se inventan registros.

Antes de persistir, usa **Editar resumen** y **Guardar como nota**. La nota resultante se guarda realmente en SQLite como `MeetingNote`, dentro del proyecto del documento si lo tiene, y conserva las referencias al documento y al análisis. Reintentar el guardado del mismo análisis devuelve la nota existente. Para cambios posteriores, abre/edita la nota guardada. Guardar un resumen no crea compromisos, milestones ni entregables.

Las propuestas de gestión continúan en el flujo existente: cada una debe revisarse por separado; aceptar una propuesta compatible registra solo esa propuesta, y rechazarla no crea registros. Los compromisos aprobados siguen usando el detector de conflictos actual.

En la sección **Notas de reunión**, el editor permite pegar texto y adjuntar PDF, DOCX, TXT o imágenes PNG/JPG/GIF/WEBP. El backend extrae texto de documentos y usa Claude para transcribir texto visible en imágenes. El usuario revisa el contenido extraído antes de guardar la nota; los archivos adjuntos originales no se guardan ni se relacionan con la nota. El chat lateral responde con Azure AI Foundry usando el borrador y todas las notas guardadas del proyecto seleccionado. Sin proyecto seleccionado, solo recibe el borrador. El contexto se limita a 60,000 caracteres; si se supera, se informa el error y no se responde con contexto incompleto.

Endpoints del asistente:

- `POST /api/notes/attachments/extract` — multipart con un campo `file`; devuelve texto extraído, sin persistir el archivo. Límite: 10 MB por documento, 5 MB por imagen.
- `POST /api/notes/chat` — recibe `projectId`, `currentNoteId`, `currentTitle`, `currentText` y `question`; no guarda la conversación ni crea compromisos.
- `POST /api/notes/analyze` — recibe `projectId` y el texto no guardado de la nota; devuelve resumen y campos respaldados con evidencia, sin persistir ni crear compromisos.
- El chat, el análisis del borrador y la extracción de texto de imágenes requieren Azure AI Foundry configurado. Si falta, la API responde `503` y la interfaz muestra el estado, sin simular respuestas.

Endpoints de IA:

- `GET /api/ai/status` — configuración disponible y nombres de opciones pendientes, sin revelar secretos.
- `POST /api/documents/{id}/analyze`
- `GET /api/documents/{id}/analyses`
- `PATCH /api/analyses/{analysisId}/proposals/{proposalId}`
- `POST /api/analyses/{analysisId}/summary-note` — guarda el título y contenido revisados por el usuario como nota enlazada.

## Desarrolladores, documentos y notas

El catálogo inicial agrega de forma idempotente estos siete desarrolladores: Lozano Velázquez Oscar Javier, Pacheco Luna Ana Karina, Honorato Mejía Alberto, Martínez Delgado Roberto Valentín, Velázquez López Juan Carlos, Dorantes Martínez Alejandra y Peña Pacheco Lisbet. No se asignan correos ni otros datos personales a estos registros. Los IDs los genera SQLite y permanecen estables; la desactivación conserva compromisos e historial. Los selectores de asignación solo reciben desarrolladores activos.

Endpoints del catálogo:

- `GET /api/developers` — desarrolladores activos para selectores.
- `GET /api/developers?includeInactive=true` — catálogo completo.
- `GET /api/developers/{id}` — detalle.
- `POST /api/developers` y `PUT /api/developers/{id}` — alta y edición.
- `PATCH /api/developers/{id}/status` — activar/desactivar sin borrar el historial.
- Los compromisos de una persona se consultan con `GET /api/commitments?developerId={id}` y sus alertas con `GET /api/developers/{id}/alerts`.

Los archivos originales PDF, DOCX y TXT (máximo 10 MB) se guardan en disco y sus metadatos/texto extraído en SQLite. El directorio se configura mediante `DocumentStorage:Directory` (variable `DocumentStorage__Directory`); por defecto es `App_Data/Documents` bajo el directorio de contenido del backend. Para desarrollo local corresponde a `src/ProjectManagement.Api/App_Data/Documents`. Esta carpeta no se versiona. Para IIS o despliegues donde la carpeta de la aplicación pueda reemplazarse, configura una ruta absoluta fuera del directorio de publicación, por ejemplo:

```powershell
$env:DocumentStorage__Directory = "D:\ProjectManagementData\Documents"
```

La identidad del Application Pool debe tener permisos de lectura/escritura en esa ubicación. Los archivos no se guardan en ubicaciones temporales; al respaldar o mover el sistema conserva tanto la base SQLite como el directorio configurado. Las notas se guardan en SQLite como registros `MeetingNote` y pueden ser generales o asociarse a un proyecto. La eliminación de documentos/notas es lógica para la base de datos: oculta el registro, borra el contenido extraído y elimina el archivo original cuando existe; el historial de análisis y propuestas permanece enlazado para mantener trazabilidad.

Endpoints principales:

- `GET /api/documents?search={texto}&projectId={id}&type={PDF|DOCX|TXT|MeetingNote}` — lista y filtros; `unassignedOnly=true` lista registros generales.
- `POST /api/documents/files` — carga archivo con `projectId` opcional.
- `POST /api/documents/notes` — crea una nota con título y contenido, con `projectId` opcional.
- `GET /api/documents/{id}` y `GET /api/documents/projects/{projectId}` — detalle y lista de proyecto.
- `GET /api/documents/{id}/file` — descarga del original (solo documentos cargados).
- `PUT /api/documents/{id}/notes` — edita una nota.
- `DELETE /api/documents/{id}` — elimina el registro de la interfaz y conserva la trazabilidad analítica.

Las notas usan el mismo flujo de análisis existente que los documentos. El backend no envía contenido a Azure AI Foundry si el endpoint y la clave no están configurados.

## Autenticación y Azure DevOps

### Arquitectura

El frontend React usa MSAL Browser y solicita un token para el scope expuesto por la API. La SPA y la API usan registros de aplicación separados: el registro SPA es público y no contiene secretos; el registro de la API es confidencial y conserva la credencial OBO en el backend. ASP.NET Core valida el JWT de la API con Microsoft Identity Web y exige identidad autenticada para las rutas `/api/*` (excepto `/api/health`). Esto no inicia una segunda sesión interactiva: la API opera en nombre del usuario con **On-Behalf-Of (OBO)** y solicita el scope delegado `499b84ac-1321-427f-aa17-267ca6975798/user_impersonation`. El SDK oficial usa ese token temporal mediante `VssOAuthAccessTokenCredential`; el token de Azure DevOps nunca se devuelve al navegador ni se guarda en SQLite. No se usa PAT, identidad de servicio como fallback ni escritura sobre work items.

La conexión se limita a la organización configurada por el administrador. `AzureDevOps:OrganizationUrl` debe ser `https://dev.azure.com/{organizacion}`; URLs arbitrarias, HTTP, credenciales en la URL, puertos distintos de HTTPS o rutas adicionales hacen fallar el inicio. Si está vacía, la integración informa que falta configuración y no simula una conexión.

### 1. Registrar la aplicación en Microsoft Entra ID

1. En **Microsoft Entra admin center → App registrations**, registra una aplicación de tipo API confidencial (por ejemplo, `ProjectManagement-Api`) en el tenant organizacional.
2. En el registro de la API, abre **Expose an API**, establece el Application ID URI `api://<API_CLIENT_ID>` y crea el scope delegado `access_as_user`. Conserva el valor completo `api://<API_CLIENT_ID>/access_as_user`.
3. En **API permissions** del registro de API, agrega **Azure DevOps → Delegated permissions → user_impersonation**, con el scope `499b84ac-1321-427f-aa17-267ca6975798/user_impersonation`.
4. Registra una segunda aplicación pública (por ejemplo, `ProjectManagement-Spa`). En **Authentication → Add a platform → Single-page application**, agrega `http://localhost:5173` para desarrollo y el origen HTTPS exacto de producción (por ejemplo, `https://<DOMINIO-PRODUCCION>`). MSAL usa el origen como URI de retorno; no se configura un callback de cookies `/signin-oidc`.
5. En el registro SPA, agrega como permiso delegado **My APIs → ProjectManagement-Api → access_as_user**. La SPA solicita únicamente este scope de la API; el navegador no solicita ni recibe el token de Azure DevOps.
6. Concede el consentimiento de administrador para los permisos delegados que lo requieran. Los usuarios deben volver a iniciar sesión después de un cambio de consentimiento.
7. En el registro confidencial de la API, configura una credencial en **Certificates & secrets** para OBO. En desarrollo usa un secreto de corta duración guardado en User Secrets. En producción, prefiere un certificado gestionado en el almacén seguro aprobado por la organización. Nunca pongas valores reales en `appsettings.json`, archivos `.env` versionados, comandos compartidos ni el repositorio.

### 2. Configurar el backend

`src/ProjectManagement.Api/appsettings.json` contiene solamente valores vacíos/no sensibles. Desde la raíz del repositorio, guarda los valores de desarrollo con User Secrets:

```powershell
dotnet user-secrets set "AzureAd:TenantId" "<TENANT_ID>" --project .\src\ProjectManagement.Api\ProjectManagement.Api.csproj
dotnet user-secrets set "AzureAd:ClientId" "<API_CLIENT_ID>" --project .\src\ProjectManagement.Api\ProjectManagement.Api.csproj
dotnet user-secrets set "AzureAd:ClientSecret" "<CONFIGURAR_EN_USER_SECRETS>" --project .\src\ProjectManagement.Api\ProjectManagement.Api.csproj
dotnet user-secrets set "AzureDevOps:OrganizationUrl" "https://dev.azure.com/<ORGANIZACION>" --project .\src\ProjectManagement.Api\ProjectManagement.Api.csproj
```

Reemplaza cada placeholder por el dato del tenant de tu organización; no ejecutes los comandos dejando los marcadores literales. El `UserSecretsId` ya está inicializado en el proyecto. El secreto se usa únicamente en el backend para el intercambio OBO. Si la política requiere certificado, configura `AzureAd:ClientCertificates` de acuerdo con Microsoft.Identity.Web (por ejemplo, origen `StoreWithThumbprint`) y da al usuario del proceso de IIS permiso de lectura de la clave privada.

Para producción, configura los mismos valores mediante el almacén de secretos del entorno o variables de entorno, no en archivos versionados:

```text
AzureAd__Instance=https://login.microsoftonline.com/
AzureAd__TenantId=<TENANT_ID>
AzureAd__ClientId=<CLIENT_ID>
AzureAd__ClientSecret=<CONFIGURAR_EN_ALMACEN_SEGURO>
AzureDevOps__OrganizationUrl=https://dev.azure.com/<ORGANIZACION>
```

Rota secretos antes de su expiración: crea y despliega el reemplazo en el almacén seguro, actualiza la referencia/configuración del backend, reinicia y verifica OBO; después revoca la credencial anterior. Para certificados, instala el certificado nuevo y autoriza temporalmente ambas claves durante el cambio, actualiza la referencia y retira el certificado viejo después de validar.

### 3. Configurar la SPA

En En `src/ProjectManagement.Web`, crea un `.env.local` no versionado con:

```text
VITE_ENTRA_TENANT_ID=<TENANT_ID>
VITE_ENTRA_CLIENT_ID=<SPA_CLIENT_ID>
VITE_API_SCOPE=api://<API_CLIENT_ID>/access_as_user
```

El archivo de ejemplo versionado está en [`src/ProjectManagement.Web/.env.example`](./src/ProjectManagement.Web/.env.example). Sustituye los placeholders, reinicia Vite tras cambiar variables y asegúrate de que `VITE_API_SCOPE` coincida con el Application ID URI y el scope expuestos en el registro de API. En producción, define estas variables en el pipeline de frontend y compila con los valores del entorno.

### 4. Conceder acceso a Azure DevOps

Registrar la aplicación y conceder el consentimiento Entra **no** agrega automáticamente a un usuario a Azure DevOps. Cada usuario necesita una cuenta con acceso a `https://dev.azure.com/<ORGANIZACION>` y permisos mínimos suficientes en la organización/proyecto para leer proyectos y work items. Comprueba el nivel de acceso/licencia y las ACL del proyecto con el administrador de Azure DevOps; la integración hereda esos permisos del usuario, no los amplía.

La organización se configura solamente en el backend con `AzureDevOps:OrganizationUrl`; no hay un campo del frontend que permita cambiar el destino.

### 5. Ejecutar y comprobar

Inicia la API y el frontend como se describe en **Inicio Rápido**, inicia sesión con Microsoft y abre **Azure DevOps**. El estado se marca conectado solo después de consultar la organización con la identidad del usuario. Selecciona un proyecto visible para cargar hasta 100 work items (el endpoint permite `top` entre 1 y 200) y abre el detalle para ver fechas y vínculos locales existentes a proyecto/compromiso. Las lecturas son en vivo; esta pantalla no importa ni persiste work items nuevos.

Endpoints (todos requieren un access token válido de la API):

- `GET /api/azure-devops/status` — prueba la conexión delegada y devuelve organización y cantidad visible de proyectos.
- `GET /api/azure-devops/projects` — proyectos visibles para el usuario.
- `GET /api/azure-devops/projects/{projectId}/work-items?top=100` — work items de un proyecto.
- `GET /api/azure-devops/work-items/{id}` — detalle y vínculos locales ya existentes.

El archivo [`src/ProjectManagement.Api/AzureDevOps.http`](./src/ProjectManagement.Api/AzureDevOps.http) contiene ejemplos manuales. `/api/health` es la única ruta de API anónima. Con tenant/client ID sin configurar, las demás rutas responden `503` en vez de permitir acceso anónimo.

### Diagnóstico

- `entra_not_configured` / aviso de configuración: configura Tenant ID, Client ID y variables Vite, y reinicia ambos servidores.
- `backend_credential_not_configured`: configura secreto/certificado en User Secrets o almacén seguro del backend.
- `consent_required` / AADSTS65001: concede el consentimiento delegado y vuelve a iniciar sesión.
- `permission_denied` o HTTP 403: el token fue aceptado, pero Azure DevOps no concedió acceso a la organización/proyecto; revisa permisos del usuario.
- HTTP 401: el JWT de la API no está presente, expiró o tiene un issuer/audience incorrecto; inicia sesión de nuevo y compara tenant, client ID y scope.
- `organization_not_found`: revisa que la URL use HTTPS, host `dev.azure.com` y el nombre correcto de organización.
- `azure_devops_network_error` / timeout: revisa DNS, salida HTTPS/proxy y disponibilidad de Azure DevOps.

No se registran tokens, cabeceras Authorization, client secrets, PAT ni contenido de documentos en las respuestas de error de Azure DevOps.

## Dashboard y seguimiento de proyectos

El Dashboard consulta `GET /api/dashboard/summary` para obtener los totales desde SQLite: proyectos activos, compromisos por estado, alertas críticas/advertencias y carga abierta por proyecto y desarrollador. El trabajo sin completar comprende los estados `Pending`, `InProgress` y `Delayed`; los vencidos tienen fecha anterior al día UTC actual, y los próximos vencen desde hoy hasta los siguientes 30 días. La distribución incluye los estados de compromiso admitidos por el sistema.

La lista y el detalle de proyectos reutilizan `GET /api/projects`. Cada respuesta incluye los conteos de compromisos pendientes, en progreso y completados, además de `productOwnerNames`. Como el modelo no asigna un Product Owner directamente al proyecto, estos nombres provienen de los miembros activos del proyecto con rol `ProductOwner` y usuario activo. El detalle conserva las secciones existentes de miembros, compromisos, milestones, entregables, alertas y Timeline/Gantt.

El Dashboard consulta los indicadores al abrirse y ofrece **Actualizar datos** para volver a cargarlos; regresar a la página tras modificar compromisos también vuelve a consultar la API.

## Licencia

Uso interno.
