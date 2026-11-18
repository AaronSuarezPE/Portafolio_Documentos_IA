using DocuIA.Api.Modelos;

namespace DocuIA.Api.Servicios;

public interface IProveedorIA
{
    bool EsDemostracion { get; }

    Task<ResultadoExtraccion> ExtraerAsync(
        ArchivoDocumento archivo,
        IReadOnlyList<string> tiposPermitidos,
        CancellationToken ct);

    Task<string> PreguntarAsync(
        ArchivoDocumento archivo,
        string pregunta,
        IReadOnlyList<MensajeChat> historial,
        CancellationToken ct);
}

/// <summary>Falla del servicio de IA (conexión, límite de uso, respuesta inválida).</summary>
public sealed class ErrorProveedorIA(string mensaje, Exception? inner = null) : Exception(mensaje, inner);

/// <summary>Regla de negocio incumplida o dato no encontrado; el mensaje es apto para mostrar a la persona.</summary>
public sealed class ErrorNegocio(string mensaje, int estado = StatusCodes.Status400BadRequest) : Exception(mensaje)
{
    public int Estado { get; } = estado;
}
