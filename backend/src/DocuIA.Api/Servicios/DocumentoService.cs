using System.Security.Cryptography;
using DocuIA.Api.Configuracion;
using DocuIA.Api.Datos;
using DocuIA.Api.Modelos;
using Microsoft.Extensions.Options;

namespace DocuIA.Api.Servicios;

public interface IDocumentoService
{
    bool EsDemostracion { get; }
    Task<IReadOnlyList<TipoDocumentoDto>> ListarTiposAsync();
    Task<DocumentoDetalleDto> ProcesarAsync(int codUsuario, string nombre, byte[] contenido, CancellationToken ct);
    Task<IReadOnlyList<DocumentoResumenDto>> ListarAsync(int codUsuario);
    Task<DocumentoDetalleDto> ObtenerAsync(int codUsuario, int codDocumento);
    Task<DocumentoDetalleDto> ConfirmarCamposAsync(int codUsuario, int codDocumento, IReadOnlyList<CampoConfirmado> campos);
    Task<RespuestaPregunta> PreguntarAsync(int codUsuario, int codDocumento, string pregunta, CancellationToken ct);
    Task EliminarAsync(int codUsuario, int codDocumento);
}

public sealed class DocumentoService(
    IDocumentoRepositorio repositorio,
    IProveedorIA ia,
    IOptions<OpcionesArchivos> opcionesArchivos,
    ILogger<DocumentoService> logger) : IDocumentoService
{
    private const int EstadoProcesado = 2;
    private const int EstadoConError = 3;
    private const int MaximoCampos = 50;
    private const int LargoMaximoPregunta = 500;
    private const int MensajesDeContexto = 6;

    public bool EsDemostracion => ia.EsDemostracion;

    public async Task<IReadOnlyList<TipoDocumentoDto>> ListarTiposAsync()
    {
        var tipos = await repositorio.ListarTiposAsync();
        return tipos.Select(t => new TipoDocumentoDto(t.CodTpoDocto, t.NomTpoDocto, t.DscTpoDocto)).ToList();
    }

    public async Task<DocumentoDetalleDto> ProcesarAsync(
        int codUsuario, string nombre, byte[] contenido, CancellationToken ct)
    {
        var validacion = ValidadorArchivo.Validar(nombre, contenido, opcionesArchivos.Value.TamanoMaximoMb);
        if (!validacion.EsValido)
            throw new ErrorNegocio(validacion.Error!);

        var nombreLimpio = Path.GetFileName(nombre);
        var hash = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();

        var codDocumento = await repositorio.AgregarAsync(
            codUsuario, nombreLimpio, validacion.TipoMime!, contenido.Length, hash, contenido);

        var tipos = await repositorio.ListarTiposAsync();
        var archivo = new ArchivoDocumento(nombreLimpio, validacion.TipoMime!, contenido);

        ResultadoExtraccion resultado;
        try
        {
            resultado = await ia.ExtraerAsync(archivo, tipos.Select(t => t.NomTpoDocto).ToList(), ct);
        }
        catch (ErrorProveedorIA)
        {
            // El documento queda guardado con estado «Con error» para que la persona vea qué pasó.
            await repositorio.ActualizarAsync(codUsuario, codDocumento, null, EstadoConError, null);
            throw;
        }

        var tipo = ElegirTipo(tipos, resultado.TipoDocumento);
        var campos = LimpiarCampos(resultado.Campos);

        await repositorio.GuardarCamposAsync(codUsuario, codDocumento, campos);
        await repositorio.ActualizarAsync(
            codUsuario, codDocumento, tipo.CodTpoDocto, EstadoProcesado, Recortar(resultado.Resumen, 1000));

        logger.LogInformation(
            "Documento {Codigo} procesado: {Campos} campos, {Tamano} bytes, demostración {Demo}.",
            codDocumento, campos.Count, contenido.Length, ia.EsDemostracion);

        return await ObtenerAsync(codUsuario, codDocumento);
    }

    public async Task<IReadOnlyList<DocumentoResumenDto>> ListarAsync(int codUsuario)
    {
        var filas = await repositorio.ListarAsync(codUsuario);
        return filas
            .Select(f => new DocumentoResumenDto(
                f.CodDocto, f.NomArchv, f.NomTpoDocto, f.NomEstdoDocto, f.NumTamno, f.FecRgtro, f.HorRgtro))
            .ToList();
    }

    public async Task<DocumentoDetalleDto> ObtenerAsync(int codUsuario, int codDocumento)
    {
        var (documento, campos) = await repositorio.ObtenerAsync(codUsuario, codDocumento);
        if (documento is null)
            throw new ErrorNegocio("El documento no existe.", StatusCodes.Status404NotFound);

        var mensajes = await repositorio.ListarMensajesAsync(codUsuario, codDocumento);

        return new DocumentoDetalleDto(
            documento.CodDocto,
            documento.NomArchv,
            documento.NomTpoDocto,
            documento.NomEstdoDocto,
            documento.NumTamno,
            documento.DscRsmen,
            documento.FecRgtro,
            documento.HorRgtro,
            campos.Select(c => new CampoDto(
                c.CodCampo, c.NomClave, c.NomEtqta, c.DscValor, c.DscValorCnfrm, (double)c.NumCnfnz)).ToList(),
            mensajes.Select(m => new MensajeDto(
                m.CodMnsje, m.NomOrgenMnsje, m.DscTexto, m.FecRgtro, m.HorRgtro)).ToList());
    }

    public async Task<DocumentoDetalleDto> ConfirmarCamposAsync(
        int codUsuario, int codDocumento, IReadOnlyList<CampoConfirmado> campos)
    {
        if (campos.Count == 0)
            throw new ErrorNegocio("No hay valores para guardar.");

        foreach (var campo in campos)
        {
            if (string.IsNullOrWhiteSpace(campo.Valor))
                throw new ErrorNegocio("Completa todos los campos antes de confirmar.");

            if (campo.Valor.Length > 1000)
                throw new ErrorNegocio("Un valor supera los 1000 caracteres.");
        }

        await ObtenerAsync(codUsuario, codDocumento);
        await repositorio.ConfirmarCamposAsync(
            codUsuario, codDocumento,
            campos.Select(c => c with { Valor = c.Valor!.Trim() }).ToList());

        return await ObtenerAsync(codUsuario, codDocumento);
    }

    public async Task<RespuestaPregunta> PreguntarAsync(
        int codUsuario, int codDocumento, string pregunta, CancellationToken ct)
    {
        pregunta = pregunta.Trim();
        if (pregunta.Length == 0)
            throw new ErrorNegocio("Escribe una pregunta.");

        if (pregunta.Length > LargoMaximoPregunta)
            throw new ErrorNegocio($"La pregunta no puede superar {LargoMaximoPregunta} caracteres.");

        var contenido = await repositorio.ObtenerContenidoAsync(codUsuario, codDocumento)
            ?? throw new ErrorNegocio("El documento no existe.", StatusCodes.Status404NotFound);

        var anteriores = await repositorio.ListarMensajesAsync(codUsuario, codDocumento);
        var historial = anteriores
            .TakeLast(MensajesDeContexto)
            .Select(m => new MensajeChat(m.CodOrgenMnsje == DocumentoRepositorio.OrigenAsistente ? "asistente" : "usuario", m.DscTexto))
            .ToList();

        var archivo = new ArchivoDocumento(contenido.NomArchv, contenido.TpoCntnd, contenido.BinCntnd);
        var respuesta = await ia.PreguntarAsync(archivo, pregunta, historial, ct);

        await repositorio.AgregarMensajeAsync(codUsuario, codDocumento, DocumentoRepositorio.OrigenUsuario, pregunta);
        await repositorio.AgregarMensajeAsync(codUsuario, codDocumento, DocumentoRepositorio.OrigenAsistente, respuesta);

        return new RespuestaPregunta(respuesta);
    }

    public async Task EliminarAsync(int codUsuario, int codDocumento)
    {
        await ObtenerAsync(codUsuario, codDocumento);
        await repositorio.EliminarAsync(codUsuario, codDocumento);
        logger.LogInformation("Documento {Codigo} eliminado por su dueño.", codDocumento);
    }

    private static FilaTipoDocumento ElegirTipo(IReadOnlyList<FilaTipoDocumento> tipos, string nombre) =>
        tipos.FirstOrDefault(t => t.NomTpoDocto.Equals(nombre?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? tipos.FirstOrDefault(t => t.NomTpoDocto.Equals("Otro", StringComparison.OrdinalIgnoreCase))
        ?? tipos[0];

    /// <summary>La respuesta de la IA no es de confianza: se recortan los textos y se descartan los campos sin clave.</summary>
    private static List<CampoExtraido> LimpiarCampos(IReadOnlyList<CampoExtraido>? campos) =>
        (campos ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Clave) && !string.IsNullOrWhiteSpace(c.Etiqueta))
            .Take(MaximoCampos)
            .Select(c => new CampoExtraido(
                Recortar(c.Clave.Trim(), 60),
                Recortar(c.Etiqueta.Trim(), 100),
                Recortar((c.Valor ?? string.Empty).Trim(), 1000),
                Math.Round(Math.Clamp(c.Confianza, 0, 1), 3)))
            .ToList();

    private static string Recortar(string? texto, int maximo) =>
        string.IsNullOrEmpty(texto) ? string.Empty : texto.Length <= maximo ? texto : texto[..maximo];
}
