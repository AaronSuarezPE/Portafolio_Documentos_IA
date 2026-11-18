using System.Security.Claims;
using DocuIA.Api.Modelos;
using DocuIA.Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocuIA.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/docuia/[controller]")]
public sealed class DocumentosController(IDocumentoService documentos) : ControllerBase
{
    private int CodUsuario => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture);

    [HttpPost("PostProcesarDocumento")]
    [EnableRateLimiting("ia")]
    [RequestSizeLimit(7 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentoDetalleDto>> PostProcesarDocumento(
        IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null)
            throw new ErrorNegocio("Adjunta un archivo en el campo «archivo».");

        await using var flujo = archivo.OpenReadStream();
        using var memoria = new MemoryStream();
        await flujo.CopyToAsync(memoria, ct);

        return Ok(await documentos.ProcesarAsync(CodUsuario, archivo.FileName, memoria.ToArray(), ct));
    }

    [HttpGet("GetList")]
    public async Task<ActionResult<IReadOnlyList<DocumentoResumenDto>>> GetList() =>
        Ok(await documentos.ListarAsync(CodUsuario));

    [HttpGet("Get")]
    public async Task<ActionResult<DocumentoDetalleDto>> Get([FromQuery] int id) =>
        Ok(await documentos.ObtenerAsync(CodUsuario, id));

    [HttpPut("PutActualizarCampos")]
    public async Task<ActionResult<DocumentoDetalleDto>> PutActualizarCampos(
        [FromQuery] int id, [FromBody] SolicitudCamposConfirmados solicitud) =>
        Ok(await documentos.ConfirmarCamposAsync(CodUsuario, id, solicitud.Campos ?? []));

    [HttpPost("PostProcesarPregunta")]
    [EnableRateLimiting("ia")]
    public async Task<ActionResult<RespuestaPregunta>> PostProcesarPregunta(
        [FromQuery] int id, [FromBody] SolicitudPregunta solicitud, CancellationToken ct) =>
        Ok(await documentos.PreguntarAsync(CodUsuario, id, solicitud.Pregunta ?? string.Empty, ct));

    [HttpDelete("DeleteEliminar")]
    public async Task<IActionResult> DeleteEliminar([FromQuery] int id)
    {
        await documentos.EliminarAsync(CodUsuario, id);
        return NoContent();
    }
}
