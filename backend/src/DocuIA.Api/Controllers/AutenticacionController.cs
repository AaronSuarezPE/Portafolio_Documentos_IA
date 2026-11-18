using DocuIA.Api.Modelos;
using DocuIA.Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocuIA.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("login")]
[Route("api/v1/docuia/[controller]")]
public sealed class AutenticacionController(IAutenticacionService autenticacion) : ControllerBase
{
    [HttpPost("PostLogin")]
    public async Task<ActionResult<RespuestaSesion>> PostLogin([FromBody] SolicitudLogin solicitud) =>
        Ok(await autenticacion.LoginAsync(solicitud));

    [HttpPost("PostAgregarUsuario")]
    public async Task<ActionResult<RespuestaSesion>> PostAgregarUsuario([FromBody] SolicitudUsuario solicitud) =>
        Ok(await autenticacion.AgregarUsuarioAsync(solicitud));

    [HttpPost("PostLoginInvitado")]
    public async Task<ActionResult<RespuestaSesion>> PostLoginInvitado() =>
        Ok(await autenticacion.LoginInvitadoAsync());
}
