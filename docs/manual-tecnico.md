# DocuIA · Manual técnico

## 1. Arquitectura

```
Navegador (Angular 19) --HTTPS + JWT--> API .NET 9 --Dapper--> SQL Server (procedimientos)
                                           |
                                           +--HTTPS, solo desde el servidor--> Gemini
```

- El navegador **nunca** llama a la IA: la clave vive solo en el servidor.
- Toda la lógica de datos está en procedimientos almacenados; la API solo los invoca.
- El contenido del documento se envía a la IA como **datos**, con una instrucción fija que ordena ignorar cualquier orden escrita dentro de él.

## 2. Estructura del repositorio

```
backend/
  database/        scripts SQL numerados + instalar.ps1 + pruebas/
  src/DocuIA.Api/  Controllers, Servicios, Datos, Modelos, Configuracion
  tests/           pruebas unitarias y de flujo completo contra una base temporal
frontend/          Angular (core, shared, features)
docs/              manuales
```

## 3. Base de datos

Estándar: nombres en minúsculas, esquema `dbo`, abreviaturas de 3 a 5 letras, identificadores asignados con `updlock, holdlock` (sin identity), indicadores `'S'/'N'`, fechas y horas como texto (`dd/mm/yyyy`, `hh:mm`), sin tablas temporales ni parámetros OUTPUT. Cada procedimiento devuelve `cod_resultado` y `dsc_mensaje`.

| Tabla | Tipo | Contenido |
|---|---|---|
| `ta_dia_mst_estdo_docto` | maestra fija | 1 Pendiente, 2 Procesado, 3 Con error |
| `ta_dia_mst_orgen_mnsje` | maestra fija | 1 Usuario, 2 Asistente |
| `ta_dia_mst_mov_tpo_docto` | maestra con movimiento | Factura, Contrato, Constancia, Otro (con auditoría `cnr_*`) |
| `ta_dia_mst_mov_usrio` | maestra con movimiento | Usuarios, clave con PBKDF2, indicador de invitado (con auditoría) |
| `ta_dia_docto` | operativa | Documento, estado, resumen, fechas |
| `ta_dia_docto_cntnd` | operativa | Archivo en binario |
| `ta_dia_docto_campo` | operativa | Dato leído, valor de la IA, valor confirmado, confianza |
| `ta_dia_docto_mnsje` | operativa | Mensajes del chat |

Las maestras fijas no llevan auditoría porque nunca cambian; las que sí varían la llevan.

Procedimientos (`pa_dia_<acción>_<entidad>`): `aggar_usrio`, `sel_usrio`, `edtar_usrio`, `lst_tpo_docto`, `aggar_docto`, `edtar_docto`, `aggar_campo`, `edtar_campo`, `lst_docto`, `sel_docto`, `sel_docto_cntnd`, `del_docto`, `aggar_mnsje`, `lst_mnsje`, `job_elmnr_docto`. Los campos y las confirmaciones viajan como parámetros de tabla (`tt_dia_campo`, `tt_dia_campo_cnfrm`).

**Instalar:**

```powershell
cd backend\database
.\instalar.ps1 -CrearBd -ConPruebas                                          # LocalDB, con datos de prueba
.\instalar.ps1 -Servidor <host> -BaseDatos <bd> -Usuario <u> -Clave <c>      # servidor remoto, base ya creada
```

`pruebas\prueba_procedimientos.sql` ejecuta los procedimientos y termina con «TODAS LAS PRUEBAS PASARON».

## 4. API

Base: `/api/v1/docuia/{controller}/{Accion}`. Todo requiere JWT salvo autenticación y salud.

| Método | Ruta | Uso |
|---|---|---|
| POST | `Autenticacion/PostLogin` | Ingresar |
| POST | `Autenticacion/PostAgregarUsuario` | Registrar |
| POST | `Autenticacion/PostLoginInvitado` | Sesión temporal |
| POST | `Documentos/PostProcesarDocumento` | Subir y analizar (multipart) |
| GET | `Documentos/GetList` | Listar los del usuario |
| GET | `Documentos/Get?id=` | Detalle, campos y mensajes |
| PUT | `Documentos/PutActualizarCampos` | Confirmar datos |
| POST | `Documentos/PostProcesarPregunta` | Pregunta al documento |
| DELETE | `Documentos/DeleteEliminar?id=` | Eliminar |
| GET | `TiposDocumento/GetCombo` | Tipos para listas |
| GET | `Salud/Get` | Estado y modo demostración |

Los errores esperados devuelven `{ "mensaje": "…" }` con 400, 401, 404 o 429; los inesperados, 500 con texto genérico y el detalle en el registro.

**Seguridad:** JWT con clave de 32+ caracteres, claves con PBKDF2, límites por minuto en login e IA, CORS por lista de orígenes, archivos validados por tamaño, extensión y firma real, y aislamiento por usuario en cada consulta.

**Registro:** `Logs/docuia-.log`, un archivo por día, 30 días.

**Limpieza:** `JobEliminarVencidos` borra documentos con más de `Limpieza:DiasRetencion` días, cada `Limpieza:HorasIntervalo` horas.

## 5. Configuración

| Clave | Descripción | Dónde |
|---|---|---|
| `ConnectionStrings:DocuIA` | Cadena a SQL Server | variable de entorno / secretos |
| `Jwt:Clave` | Clave de firma (obligatoria) | variable de entorno / secretos |
| `Gemini:ApiKey` | Clave de IA; vacía = modo demostración | variable de entorno / secretos |
| `Gemini:Modelo` | Modelo (por defecto `gemini-2.5-flash`) | appsettings |
| `Archivos:TamanoMaximoMb` | Máximo por archivo | appsettings |
| `Cors:Origenes` | Orígenes permitidos del frontend | appsettings / entorno |

En variables de entorno, los niveles se separan con doble guion bajo (`Jwt__Clave`). Las claves nunca se versionan.

## 6. Frontend

**Avisos:** un solo `AvisosService` (`exito`, `error`, `info`, `confirmar`) abre un cuadro centrado (`DialogoAvisoComponent`) con ícono, título, mensaje y botón; no se usa `alert`, `confirm`, `prompt` ni snackbars. Los formularios nuevos usan `app-panel-lateral` (entra por la derecha). Los archivos se validan en el navegador (extensión, tamaño, vacío y firma real de los primeros bytes) y el servidor repite las mismas reglas con los mismos textos.

Angular 19 con componentes standalone, rutas perezosas, `inject()`, signals y formularios reactivos. `core/` tiene servicios, interceptor, guards y validadores; `shared/` el panel lateral y el diálogo de confirmación; `features/` las pantallas. La dirección de la API sale de `src/environments`.

Los formularios validan en el navegador antes de llamar al servidor: obligatorios marcados con asterisco, marcador de ejemplo en cada campo, archivos revisados (extensión, tamaño, vacío) y ningún fallo en silencio. Al fallar, el foco va al primer error.

## 7. Pruebas

Un solo comando corre todo y termina con error si algo falla:

```powershell
.\pruebas-completas.ps1             # todas las pruebas
.\pruebas-completas.ps1 -Capas 1    # solo tests y linters
```

| Qué | Para qué sirve | Herramienta | Resultado |
|---|---|---|---|
| Tests y linters | Procedimientos SQL, compilación sin advertencias, pruebas del backend y del frontend, ESLint | script SQL, xUnit, Karma/Jasmine, angular-eslint | 156 pruebas de backend, 58 de frontend, lint sin errores |
| Mutation testing | Comprobar que las pruebas detectan errores: se altera el código a propósito y alguna prueba debe fallar | Stryker.NET (backend) y StrykerJS con Vitest (frontend) | backend 96,58 %, frontend 79 % (mínimo: 70 %) |
| Pruebas E2E | El flujo real: navegador, API y base de datos | Playwright con Chrome | 6 pruebas |

- **Mutation testing:** en el backend se aplica a la lógica (autenticación, claves, documentos, tokens, validador de archivos y proveedor de demostración); los controladores, la conexión y los procesos de fondo quedan fuera. En el frontend, a `core/` (formato, validadores y errores). Los mutantes que sobreviven son equivalentes (por ejemplo, `?? string.Empty` sobre un texto que ya sería vacío). Los informes salen en `StrykerOutput/` y `frontend/reports/mutacion/`, que no se suben al repositorio.
- **E2E:** acceso, registro, subida de un PDF, corrección de datos, chat, eliminación, rechazo de Excel y de archivos falsos, y aislamiento entre invitados. Necesitan la base instalada (`backend\database\instalar.ps1 -CrearBd`); el script levanta y apaga la API y el frontend.
- **Analizadores de .NET:** `Directory.Build.props` y `.editorconfig` hacen que toda advertencia rompa la compilación. Detectaron formatos de números y fechas que dependían de la cultura del servidor; ahora usan `CultureInfo.InvariantCulture`.

**Backend (156, xUnit contra una base temporal que se crea y elimina sola):**
- Unitarias: validación de archivos (firma real, tamaño, extensión) y claves.
- Flujo completo: registro, login, subida, confirmación, chat, borrado, aislamiento entre usuarios.
- Casos de borde (`CasosLimiteApiTests`): 13 formatos no permitidos (Excel, Word, PowerPoint, zip, txt, exe, html, gif, webp, sin extensión) con mensaje que explica qué hacer; Excel o zip renombrado como pdf/png/jpg; imagen con extensión de otra; 5 MB exactos aceptados y 5 MB + 1 byte rechazado; archivo vacío; petición sin archivo; nombre con ruta (`..\`); mismo nombre dos veces; límite de 20 documentos; confirmación con valor vacío, nulo, de 1001 caracteres y lista vacía; campo que no pertenece al documento; identificadores 0, -1, inexistente y no numérico; token manipulado; todas las rutas protegidas sin sesión; invitados aislados; pregunta de 500 caracteres y con instrucciones trampa; documento eliminado; registro con datos vacíos, cortos o demasiado largos; mismo mensaje para correo inexistente y clave incorrecta; la respuesta nunca incluye clave ni hash.
- Procedimientos SQL: `database/pruebas/prueba_procedimientos.sql`.

**Frontend (58, Jasmine/Karma en Chrome sin pantalla):** validadores (extensión, tamaño, vacío, firma real, mensajes por tipo), mensajes de error HTTP, interceptor (token solo a la API, 401 con aviso de sesión vencida), acceso (login, registro, invitado, errores del servidor), lista y panel de subida (rechazos, varios archivos, éxito, error, eliminar, cerrar con archivo), detalle (validaciones, guardado, preguntas, eliminar, documento inexistente) y el servicio de avisos.

## 8. Despliegue

- **Base de datos y API:** MonsterASP.NET (SQL Server y hosting .NET). Se instala la base con `instalar.ps1` apuntando al servidor remoto (sin `-CrearBd`), se publica con `dotnet publish -c Release` y se sube el contenido a la raíz del sitio. La configuración (`ConnectionStrings:DocuIA`, `Jwt:Clave`, `Gemini:ApiKey`, `Cors:Origenes`) va en un `appsettings.Production.json` que no se sube al repositorio.
- **Frontend:** Netlify. Se ajusta `apiUrl` en `src/environments/environment.ts`, se ejecuta `ng build` y se publica `dist/frontend/browser`. El archivo `_redirects` envía todas las rutas a `index.html`.
- **Swagger:** la API publica su documentación en la raíz (`/swagger`).
- **Hospedaje compartido:** algunos bloquean PUT y DELETE con un firewall. Si al confirmar datos o eliminar aparece un error de CORS sin registro en la API, hay que revisar esa regla.

## 9. Decisiones y límites

- Dapper y procedimientos en lugar de un ORM, para mantener el control del SQL y el estándar de base.
- IA con salida estructurada y tipos de documento tomados de la base, para que el resultado siempre sea validable.
- Los documentos se guardan en la base por simplicidad y se eliminan a los 7 días; para volúmenes mayores convendría almacenamiento de objetos.
- El modo demostración permite probar todo el flujo sin clave ni costo.
