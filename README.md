# DocuIA

Aplicación web que lee documentos con inteligencia artificial. Subes una factura, un contrato o una constancia (PDF, PNG o JPG), la IA extrae los datos clave, los revisas y corriges, y después puedes hacerle preguntas al documento en un chat.

Proyecto de portafolio de Aarón Suárez.

## Demostración en línea

- Aplicación: https://docuia.pages.dev (se puede entrar como invitado, sin registro)
- API y documentación Swagger: https://asportafolioproy01.runasp.net

## Tecnologías

| Capa | Tecnología |
|---|---|
| Frontend | Angular 19, Angular Material, componentes standalone, signals, formularios reactivos |
| Backend | .NET 9, ASP.NET Core (Controllers), Dapper, JWT, Serilog, Swagger |
| Base de datos | SQL Server, todo con procedimientos almacenados |
| IA | Gemini por API REST, llamada solo desde el servidor |
| Publicación | Backend y base en MonsterASP.NET, frontend en Cloudflare Pages |

Sin clave de Gemini la aplicación funciona en **modo demostración**, con datos de ejemplo.

## Qué se puede ver en el código

| Tema | Dónde |
|---|---|
| Formularios con validación completa (campos y archivos) | `frontend/src/app/features`, `core/validadores.ts` |
| Sesión con JWT, guards e interceptor | `frontend/src/app/core` |
| API REST con rutas versionadas y límites de uso | `backend/src/DocuIA.Api/Controllers` |
| IA con respuesta estructurada y defensa ante instrucciones ocultas en el documento | `Servicios/ProveedorGemini.cs` |
| Validación de archivos por su firma real, no solo por la extensión | `Servicios/ValidadorArchivo.cs` |
| Procedimientos almacenados y su script de pruebas | `backend/database` |
| Borrado automático de datos vencidos | `Servicios/JobEliminarVencidos.cs` |

## Estructura

```
backend/
  database/   scripts SQL numerados e instalador
  src/        API (.NET 9)
  tests/      pruebas del backend (xUnit)
frontend/     aplicación Angular, con pruebas y E2E
docs/         manual de usuario y manual técnico
```

## Cómo correrlo en local

Requisitos: .NET 9 SDK, Node 20 o superior, SQL Server LocalDB (o cualquier SQL Server) y `sqlcmd`.

```powershell
# 1. Base de datos (crea "docuia" en LocalDB, con datos de prueba)
cd backend\database
.\instalar.ps1 -CrearBd -ConPruebas

# 2. API en http://localhost:5234
cd ..\src\DocuIA.Api
dotnet user-secrets set "Jwt:Clave" "<una clave aleatoria de 32 caracteres o más>"
dotnet run

# 3. Frontend en http://localhost:4200
cd ..\..\..\frontend
npm install
npm start
```

Para usar la IA real, crea una clave gratuita en Google AI Studio y guárdala con `dotnet user-secrets set "Gemini:ApiKey" "<clave>"`.

## Pruebas

```powershell
.\pruebas-completas.ps1             # todas
.\pruebas-completas.ps1 -Capas 1    # solo tests y linters
```

- 156 pruebas de backend, 58 de frontend y 6 de punta a punta (Playwright).
- Mutation testing con Stryker: 96,58 % en el backend y 79 % en el frontend.
- ESLint y analizadores de .NET sin advertencias.

El detalle está en el [manual técnico](docs/manual-tecnico.md).

## Documentación

- [Manual de usuario](docs/manual-usuario.md)
- [Manual técnico](docs/manual-tecnico.md)

## Autor

Aarón Suárez, desarrollador frontend (Angular).
