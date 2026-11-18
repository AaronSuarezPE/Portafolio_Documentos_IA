using Microsoft.AspNetCore.Diagnostics;

namespace DocuIA.Api.Servicios;

/// <summary>
/// Convierte las excepciones en respuestas JSON con un mensaje claro.
/// Los errores esperados (reglas de negocio, IA caída) no llenan el log con trazas; los inesperados sí.
/// </summary>
public sealed class ManejadorExcepciones(ILogger<ManejadorExcepciones> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        var (estado, mensaje) = excepcion switch
        {
            ErrorNegocio e => (e.Estado, e.Message),
            ErrorProveedorIA e => (StatusCodes.Status502BadGateway, e.Message),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "La solicitud no es válida o el archivo es demasiado grande."),
            _ => (StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado. Inténtalo de nuevo."),
        };

        if (estado >= 500 && excepcion is not ErrorProveedorIA)
            logger.LogError(excepcion, "Error al atender {Metodo} {Ruta}.", contexto.Request.Method, contexto.Request.Path);
        else
            logger.LogWarning("{Metodo} {Ruta} respondió {Estado}: {Mensaje}",
                contexto.Request.Method, contexto.Request.Path, estado, mensaje);

        contexto.Response.StatusCode = estado;
        await contexto.Response.WriteAsJsonAsync(new { mensaje }, ct);
        return true;
    }
}
