using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using DocuIA.Api.Configuracion;
using DocuIA.Api.Datos;
using DocuIA.Api.Servicios;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

// Las columnas llegan como cod_docto y las clases tienen CodDocto.
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((contexto, log) => log
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    // ManejadorExcepciones ya deja cada error en el log con el nivel que corresponde
    .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogEventLevel.Fatal)
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "Logs", "docuia-.log"),
        formatProvider: System.Globalization.CultureInfo.InvariantCulture,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} | {Level:u3} | {SourceContext} | {Message:lj} {Exception}{NewLine}"));

var configuracion = builder.Configuration;
var servicios = builder.Services;

servicios.Configure<OpcionesGemini>(configuracion.GetSection(OpcionesGemini.Seccion));
servicios.Configure<OpcionesArchivos>(configuracion.GetSection(OpcionesArchivos.Seccion));
servicios.Configure<OpcionesJwt>(configuracion.GetSection(OpcionesJwt.Seccion));
servicios.Configure<OpcionesLimpieza>(configuracion.GetSection(OpcionesLimpieza.Seccion));

// Acceso a datos y servicios de negocio
servicios.AddSingleton<IFabricaConexion, FabricaConexion>();
servicios.AddSingleton<IUsuarioRepositorio, UsuarioRepositorio>();
servicios.AddSingleton<IDocumentoRepositorio, DocumentoRepositorio>();
servicios.AddSingleton<IClaveService, ClaveService>();
servicios.AddSingleton<ITokenService, TokenService>();
servicios.AddScoped<IAutenticacionService, AutenticacionService>();
servicios.AddScoped<IDocumentoService, DocumentoService>();
servicios.AddHostedService<JobEliminarVencidos>();

// Sin clave de IA se usa el modo demostración; con clave, Gemini.
servicios.AddHttpClient<ProveedorGemini>(c => c.Timeout = TimeSpan.FromSeconds(90));
servicios.AddScoped<IProveedorIA>(sp =>
    sp.GetRequiredService<IOptions<OpcionesGemini>>().Value.Habilitado
        ? sp.GetRequiredService<ProveedorGemini>()
        : new ProveedorDemostracion());

// Autenticación con JWT
servicios.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
servicios.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<OpcionesJwt>>((jwt, opciones) =>
    {
        var o = opciones.Value;
        if (o.Clave.Length < 32)
            throw new InvalidOperationException("Configura Jwt:Clave con al menos 32 caracteres (variable Jwt__Clave o user-secrets).");

        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = o.Emisor,
            ValidAudience = o.Audiencia,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Clave)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
servicios.AddAuthorization();
servicios.AddExceptionHandler<ManejadorExcepciones>();

// CORS: solo los sitios indicados en la configuración
var origenes = configuracion.GetSection("Cors:Origenes").Get<string[]>() ?? [];
servicios.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(origenes).AllowAnyHeader().WithMethods("GET", "POST", "PUT", "DELETE")));

// Límites de uso: cuidan la cuota gratuita de la IA y frenan la fuerza bruta en el login
var limiteLogin = configuracion.GetValue("Limites:LoginPorMinuto", 10);
var limiteIa = configuracion.GetValue("Limites:IaPorMinuto", 10);
servicios.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (contexto, ct) =>
        await contexto.HttpContext.Response.WriteAsJsonAsync(
            new { mensaje = "Hiciste demasiadas solicitudes. Espera un minuto e inténtalo de nuevo." }, ct);

    o.AddPolicy("login", contexto => RateLimitPartition.GetFixedWindowLimiter(
        $"ip:{contexto.Connection.RemoteIpAddress}",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteLogin, Window = TimeSpan.FromMinutes(1) }));

    o.AddPolicy("ia", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } id ? $"u:{id}" : $"ip:{contexto.Connection.RemoteIpAddress}",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteIa, Window = TimeSpan.FromMinutes(1) }));
});

// Swagger: documentación y pruebas de la API (botón «Authorize» para pegar el token de la sesión)
servicios.AddEndpointsApiExplorer();
servicios.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "DocuIA API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
        Description = "Pega el token que devuelve Autenticacion/PostLogin (sin la palabra Bearer)."
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

servicios.AddControllers().ConfigureApiBehaviorOptions(o =>
    o.InvalidModelStateResponseFactory = _ =>
        new BadRequestObjectResult(new { mensaje = "La solicitud no es válida." }));

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 8 * 1024 * 1024);

var app = builder.Build();

if (string.IsNullOrWhiteSpace(configuracion.GetConnectionString("DocuIA")))
    throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:DocuIA.");

app.UseSerilogRequestLogging();

app.UseExceptionHandler(_ => { });

app.UseSwagger();
app.UseSwaggerUI(o => o.RoutePrefix = "swagger");
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Logger.LogInformation("DocuIA iniciada. Modo demostración: {Demo}.",
    !app.Services.GetRequiredService<IOptions<OpcionesGemini>>().Value.Habilitado);

app.Run();

public partial class Program;
