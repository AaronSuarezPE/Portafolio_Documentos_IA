using DocuIA.Api.Modelos;
using DocuIA.Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocuIA.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/docuia/[controller]")]
public sealed class TiposDocumentoController(IDocumentoService documentos) : ControllerBase
{
    [HttpGet("GetCombo")]
    public async Task<ActionResult<IReadOnlyList<TipoDocumentoDto>>> GetCombo() =>
        Ok(await documentos.ListarTiposAsync());
}
