namespace DocuIA.Api.Modelos;

// Resultado de la IA ------------------------------------------------------------

public sealed record CampoExtraido(string Clave, string Etiqueta, string Valor, double Confianza);

public sealed record ResultadoExtraccion(
    string TipoDocumento,
    string Resumen,
    IReadOnlyList<CampoExtraido> Campos);

public sealed record ArchivoDocumento(string Nombre, string TipoMime, byte[] Contenido);

public sealed record MensajeChat(string Rol, string Texto);

// Solicitudes -------------------------------------------------------------------

public sealed record SolicitudLogin(string? Email, string? Clave);

public sealed record SolicitudUsuario(string? Nombre, string? Email, string? Clave);

public sealed record SolicitudPregunta(string? Pregunta);

public sealed record CampoConfirmado(int CodCampo, string? Valor);

public sealed record SolicitudCamposConfirmados(IReadOnlyList<CampoConfirmado>? Campos);

// Respuestas --------------------------------------------------------------------

public sealed record RespuestaSesion(string Token, string Nombre, bool EsInvitado, int MinutosVigencia);

public sealed record TipoDocumentoDto(int CodTipo, string Nombre, string Descripcion);

public sealed record DocumentoResumenDto(
    int CodDocumento,
    string NombreArchivo,
    string Tipo,
    string Estado,
    int TamanoBytes,
    string Fecha,
    string Hora);

public sealed record CampoDto(
    int CodCampo,
    string Clave,
    string Etiqueta,
    string Valor,
    string? ValorConfirmado,
    double Confianza);

public sealed record MensajeDto(int CodMensaje, string Origen, string Texto, string Fecha, string Hora);

public sealed record DocumentoDetalleDto(
    int CodDocumento,
    string NombreArchivo,
    string Tipo,
    string Estado,
    int TamanoBytes,
    string Resumen,
    string Fecha,
    string Hora,
    IReadOnlyList<CampoDto> Campos,
    IReadOnlyList<MensajeDto> Mensajes);

public sealed record RespuestaPregunta(string Respuesta);
